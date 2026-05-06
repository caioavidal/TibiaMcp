using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using TibiaMcp.Server.Models;

namespace TibiaMcp.Server.Services;

/// <summary>
/// Fetches creature data from the Tibia Fandom Wiki in real time.
/// Results are cached in memory because the wiki content changes infrequently.
/// </summary>
public partial class CreatureWikiService
{
    private const string WikiBaseUrl = "https://tibia.fandom.com";
    private const string ApiBaseUrl = "https://tibia.fandom.com/api.php";

    // Cache keys
    private const string CacheKeyListing = "creatures:listing";
    private const string CacheKeyDetailPrefix = "creatures:detail:";

    // Cache duration – wiki content is stable, 2 hours is safe
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(2);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CreatureWikiService> _logger;

    // Cloudflare cookie management
    private DateTime _cookieExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _warmupLock = new(1, 1);

    public CreatureWikiService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<CreatureWikiService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the full creature listing from the wiki (List_of_Creatures).
    /// Returns name, HP, exp, and version for every creature.
    /// Results are cached in memory for <see cref="CacheDuration"/>.
    /// </summary>
    public async Task<List<Creature>> GetListingAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue<List<Creature>>(CacheKeyListing, out var cached))
            return cached!;

        var doc = await FetchPageViaApiAsync("List_of_Creatures", ct);
        if (doc == null) return [];

        var results = ParseListingPage(doc);

        _cache.Set(CacheKeyListing, results, CacheDuration);
        _logger.LogDebug("Cached creature listing ({Count} items) for {Duration}",
            results.Count, CacheDuration);

        return results;
    }

    /// <summary>
    /// Fetches a single creature by name, including its full infobox and sections.
    /// Results are cached in memory for <see cref="CacheDuration"/>.
    /// </summary>
    public async Task<Creature?> GetCreatureByNameAsync(string name, CancellationToken ct = default)
    {
        var pageName = name.Replace(' ', '_');
        var cacheKey = $"{CacheKeyDetailPrefix}{pageName}";

        if (_cache.TryGetValue<Creature>(cacheKey, out var cached))
            return cached;

        var url = $"{WikiBaseUrl}/wiki/{Uri.EscapeDataString(pageName)}";

        var doc = await FetchPageViaApiAsync(pageName, ct);
        if (doc == null) return null;

        var result = ParseDetailPage(doc, name, url);

        _cache.Set(cacheKey, result, CacheDuration);
        _logger.LogDebug("Cached creature '{Name}' for {Duration}", name, CacheDuration);

        return result;
    }

    /// <summary>
    /// Searches creatures by name from the full listing.
    /// </summary>
    public async Task<List<Creature>> SearchCreaturesAsync(
        string? search = null,
        CancellationToken ct = default)
    {
        var all = await GetListingAsync(ct);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLowerInvariant();
            all = all.Where(c =>
                c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return all;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Cloudflare bypass
    // ──────────────────────────────────────────────────────────────────────

    private async Task EnsureCookieAsync(CancellationToken ct)
    {
        if (_cookieExpiry != DateTime.MinValue &&
            DateTime.UtcNow < _cookieExpiry.AddMinutes(-5))
            return;

        await _warmupLock.WaitAsync(ct);
        try
        {
            if (_cookieExpiry != DateTime.MinValue &&
                DateTime.UtcNow < _cookieExpiry.AddMinutes(-5))
                return;

            _logger.LogDebug("Refreshing Cloudflare cookie via warm-up request…");
            using var response = await _httpClient.GetAsync(WikiBaseUrl, ct);
            _cookieExpiry = DateTime.UtcNow.AddMinutes(25);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Warm-up request failed (non-fatal)");
        }
        finally
        {
            _warmupLock.Release();
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  HTTP / HTML helpers
    // ──────────────────────────────────────────────────────────────────────

    private async Task<HtmlDocument?> FetchPageViaApiAsync(string pageName, CancellationToken ct)
    {
        await EnsureCookieAsync(ct);

        var apiUrl = $"{ApiBaseUrl}?action=parse&page={Uri.EscapeDataString(pageName)}&format=json&prop=text&redirects=";

        try
        {
            _logger.LogDebug("Fetching wiki page via API: {PageName}", pageName);

            var response = await _httpClient.GetAsync(apiUrl, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var jsonDoc = JsonDocument.Parse(json);

            if (!jsonDoc.RootElement.TryGetProperty("parse", out var parse))
            {
                _logger.LogWarning("API response missing 'parse' property for {PageName}", pageName);
                return null;
            }

            var html = parse.GetProperty("text").GetProperty("*").GetString();
            if (string.IsNullOrWhiteSpace(html))
            {
                _logger.LogWarning("API returned empty content for {PageName}", pageName);
                return null;
            }

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml($"<div class=\"mw-parser-output\">{html}</div>");
            return htmlDoc;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching {PageName} via API", pageName);
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Request timed out for {PageName}", pageName);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON response for {PageName}", pageName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching {PageName}", pageName);
            return null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Listing page parsing  (List_of_Creatures)
    // ──────────────────────────────────────────────────────────────────────

    private List<Creature> ParseListingPage(HtmlDocument doc)
    {
        var results = new List<Creature>();

        var parser = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'mw-parser-output')]");
        if (parser == null) return results;

        // The listing is a single table with columns: Name | (icon) | HP | Exp | Version
        var table = parser.SelectSingleNode(".//table");
        if (table == null) return results;

        var rows = table.SelectNodes(".//tr");
        if (rows == null || rows.Count < 2) return results;

        foreach (var row in rows.Skip(1))
        {
            var cells = row.SelectNodes("./td");
            if (cells == null || cells.Count < 4) continue;

            // Name (column 0) — the text link, not the icon link
            var nameLink = cells[0]
                .SelectNodes(".//a")?
                .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.InnerText));
            var name = WebUtility.HtmlDecode(nameLink?.InnerText.Trim() ?? cells[0].InnerText.Trim());
            var href = nameLink?.GetAttributeValue("href", "");
            var fullUrl = string.IsNullOrWhiteSpace(href)
                ? $"{WikiBaseUrl}/wiki/{Uri.EscapeDataString(name)}"
                : $"{WikiBaseUrl}{href}";

            // HP (column 2)
            var hp = WebUtility.HtmlDecode(cells[2].InnerText.Trim());
            if (string.IsNullOrWhiteSpace(hp) || hp == "?") hp = null;

            // Exp (column 3)
            var exp = WebUtility.HtmlDecode(cells[3].InnerText.Trim());
            if (string.IsNullOrWhiteSpace(exp) || exp == "?") exp = null;

            // Version (column 4)
            string? version = null;
            if (cells.Count >= 5)
            {
                version = WebUtility.HtmlDecode(cells[4].InnerText.Trim());
                if (string.IsNullOrWhiteSpace(version)) version = null;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                results.Add(new Creature
                {
                    Name = SanitizeText(name),
                    Url = fullUrl,
                    Hp = hp,
                    Exp = exp,
                    Version = version
                });
            }
        }

        return results;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Detail page parsing
    // ──────────────────────────────────────────────────────────────────────

    private static Creature ParseDetailPage(HtmlDocument doc, string name, string url)
    {
        var parser = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'mw-parser-output')]");

        // Extract infobox values first
        string? hp = null;
        string? exp = null;
        int? speed = null;
        string? maxDamage = null;
        string? summon = null;
        string? convince = null;
        string? classification = null;
        string? spawnType = null;
        bool? illusionable = null;
        bool? pushable = null;
        bool? pushes = null;
        string? paralysable = null;
        bool? senseInvisibility = null;
        string? walksThrough = null;
        string? version = null;
        string? status = null;

        if (parser != null)
        {
            var aside = parser.SelectSingleNode(".//aside");
            if (aside != null)
            {
                hp = GetInfoBoxValue(aside, "hp");
                exp = GetInfoBoxValue(aside, "exp");
                speed = ParseInt(GetInfoBoxValue(aside, "speed"));
                maxDamage = GetInfoBoxValue(aside, "maxdmg");
                summon = GetInfoBoxValue(aside, "summon");
                convince = GetInfoBoxValue(aside, "convince");
                classification = GetInfoBoxValue(aside, "primarytype");
                spawnType = GetInfoBoxValue(aside, "spawntype");

                var illVal = GetInfoBoxValue(aside, "illusionable");
                if (illVal != null) illusionable = illVal.Contains("✓");

                var pushVal = GetInfoBoxValue(aside, "pushable");
                if (pushVal != null) pushable = pushVal.Contains("✓");

                var pushObjVal = GetInfoBoxValue(aside, "pushobjects");
                if (pushObjVal != null) pushes = pushObjVal.Contains("✓");

                paralysable = GetInfoBoxValue(aside, "paraimmune");

                var senseVal = GetInfoBoxValue(aside, "senseinvis");
                if (senseVal != null) senseInvisibility = senseVal.Contains("✓");

                walksThrough = GetInfoBoxValue(aside, "walksthrough");
                version = GetInfoBoxValue(aside, "implemented");
                status = GetInfoBoxValue(aside, "status");
            }
        }

        var detailedDescription = ExtractIntroParagraph(doc);
        var rawSections = ExtractSections(doc);

        return new Creature
        {
            Name = name,
            Url = url,
            Hp = hp,
            Exp = exp,
            Classification = classification,
            Speed = speed,
            MaxDamage = maxDamage,
            Summon = summon,
            Convince = convince,
            SpawnType = spawnType,
            Illusionable = illusionable,
            Pushable = pushable,
            Pushes = pushes,
            Paralysable = paralysable,
            SenseInvisibility = senseInvisibility,
            WalksThrough = walksThrough,
            Version = version,
            Status = status,
            DetailedDescription = detailedDescription,
            Sections = rawSections
                .Select((s, i) => new CreatureSection
                {
                    Heading = SanitizeText(s.Heading),
                    HeadingId = s.HeadingId,
                    Content = SanitizeText(s.Content),
                    SortOrder = i
                })
                .ToList()
        };
    }

    /// <summary>
    /// Gets a value from the portable infobox by <c>data-source</c> attribute.
    /// </summary>
    private static string? GetInfoBoxValue(HtmlNode aside, string dataSource)
    {
        var div = aside.SelectSingleNode($".//div[@data-source='{dataSource}']");
        if (div == null) return null;

        var valueDiv = div.SelectSingleNode(".//div[contains(@class,'pi-data-value')]");
        var value = valueDiv != null
            ? WebUtility.HtmlDecode(valueDiv.InnerText.Trim())
            : WebUtility.HtmlDecode(div.InnerText.Trim());

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Intro / Sections (shared with other services)
    // ──────────────────────────────────────────────────────────────────────

    internal static string? ExtractIntroParagraph(HtmlDocument doc)
    {
        var parser = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'mw-parser-output')]");
        if (parser == null) return null;

        var paragraphs = parser.SelectNodes(".//p[@class != 'mw-empty-elt']");
        if (paragraphs != null)
        {
            foreach (var p in paragraphs)
            {
                if (p.Ancestors("aside").Any() || p.Ancestors("table").Any())
                    continue;

                var text = WebUtility.HtmlDecode(p.InnerText.Trim());
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 10)
                    return text;
            }
        }

        var aside = parser.SelectSingleNode(".//aside");
        var startNode = aside?.NextSibling ?? parser.FirstChild;

        var introParts = new List<string>();
        while (startNode != null)
        {
            if (startNode is { NodeType: HtmlNodeType.Element } e &&
                (e.Name is "h2" or "h3" or "h4" or "table"))
                break;

            if (startNode.NodeType == HtmlNodeType.Text ||
                startNode is { NodeType: HtmlNodeType.Element } elem &&
                elem.Name is not ("p" or "aside" or "table" or "h2" or "h3" or "h4" or "div" or "ul" or "ol"))
            {
                var text = WebUtility.HtmlDecode(startNode.InnerText.Trim());
                if (!string.IsNullOrWhiteSpace(text))
                    introParts.Add(text);
            }

            startNode = startNode.NextSibling;
        }

        var combined = string.Join(" ", introParts);
        return combined.Length > 10 ? combined : null;
    }

    internal static List<(string Heading, string? HeadingId, string Content)> ExtractSections(HtmlDocument doc)
    {
        var sections = new List<(string, string?, string)>();
        var parser = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'mw-parser-output')]");
        if (parser == null) return sections;

        var headings = parser.SelectNodes(".//h2 | .//h3");
        if (headings == null) return sections;

        foreach (var headingNode in headings)
        {
            var span = headingNode.SelectSingleNode(".//span[contains(@class,'mw-headline')]");
            if (span == null) continue;

            var headingText = WebUtility.HtmlDecode(span.InnerText.Trim());
            string? headingId = span.GetAttributeValue("id", string.Empty);
            if (string.IsNullOrEmpty(headingId)) headingId = null;

            if (string.IsNullOrWhiteSpace(headingText)) continue;
            if (headingText.Equals("See Also", StringComparison.OrdinalIgnoreCase)) continue;
            if (headingText.Equals("References", StringComparison.OrdinalIgnoreCase)) continue;
            if (headingText.Equals("External Links", StringComparison.OrdinalIgnoreCase)) continue;

            var contentParts = new List<string>();
            var next = headingNode.NextSibling;
            while (next != null && next.Name != "h2" && next.Name != "h3")
            {
                if (next.NodeType == HtmlNodeType.Element &&
                    next.Name != "table")
                {
                    var text = WebUtility.HtmlDecode(next.InnerText.Trim());
                    if (!string.IsNullOrWhiteSpace(text))
                        contentParts.Add(text);
                }
                next = next.NextSibling;
            }

            var fullContent = string.Join("\n", contentParts);
            if (!string.IsNullOrWhiteSpace(fullContent))
            {
                sections.Add((headingText, headingId, fullContent));
            }
        }

        return sections;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────────

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = IntCleanRegex().Replace(value, "");
        if (int.TryParse(cleaned, out var result))
            return result;
        return null;
    }

    private static string SanitizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^\d-]")]
    private static partial Regex IntCleanRegex();
}
