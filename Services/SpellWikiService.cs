using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using TibiaMcp.Server.Models;

namespace TibiaMcp.Server.Services;

/// <summary>
/// Fetches spell data from the Tibia Fandom Wiki in real time.
/// Results are cached in memory because the wiki content changes infrequently.
/// </summary>
public partial class SpellWikiService
{
    private const string WikiBaseUrl = "https://tibia.fandom.com";
    private const string ApiBaseUrl = "https://tibia.fandom.com/api.php";

    // Cache keys
    private const string CacheKeyListing = "spells:listing";
    private const string CacheKeyDetailPrefix = "spells:detail:";

    // Cache duration – wiki content is stable, 2 hours is safe
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(2);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SpellWikiService> _logger;

    // Cloudflare cookie management
    private DateTime _cookieExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _warmupLock = new(1, 1);

    public SpellWikiService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<SpellWikiService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the full spell listing (instant spells) from the wiki.
    /// Results are cached in memory for <see cref="CacheDuration"/>.
    /// </summary>
    public async Task<List<Spell>> GetListingAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue<List<Spell>>(CacheKeyListing, out var cached))
            return cached!;

        var doc = await FetchPageViaApiAsync("Spells", ct);
        if (doc == null) return [];

        var results = ParseListingPage(doc);

        _cache.Set(CacheKeyListing, results, CacheDuration);
        _logger.LogDebug("Cached spell listing ({Count} items) for {Duration}",
            results.Count, CacheDuration);

        return results;
    }

    /// <summary>
    /// Fetches a single spell by name, including its detailed description and sections.
    /// Results are cached in memory for <see cref="CacheDuration"/>.
    /// </summary>
    public async Task<Spell?> GetSpellByNameAsync(string name, CancellationToken ct = default)
    {
        var pageName = name.Replace(' ', '_');
        var cacheKey = $"{CacheKeyDetailPrefix}{pageName}";

        if (_cache.TryGetValue<Spell>(cacheKey, out var cached))
            return cached;

        var url = $"{WikiBaseUrl}/wiki/{Uri.EscapeDataString(pageName)}";

        var doc = await FetchPageViaApiAsync(pageName, ct);
        if (doc == null) return null;

        var result = ParseDetailPage(doc, name, pageName, url);

        _cache.Set(cacheKey, result, CacheDuration);
        _logger.LogDebug("Cached spell '{Name}' for {Duration}", name, CacheDuration);

        return result;
    }

    /// <summary>
    /// Searches the spell listing by words (the magic phrase, e.g. "exori gran ico").
    /// </summary>
    public async Task<Spell?> GetSpellByWordsAsync(string words, CancellationToken ct = default)
    {
        var all = await GetListingAsync(ct);
        // Normalize: lowercase, collapse whitespace
        var normalized = SanitizeText(words.ToLowerInvariant());
        var match = all.FirstOrDefault(s =>
            SanitizeText(s.Words.ToLowerInvariant()).Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (match == null) return null;

        return await GetSpellByNameAsync(match.Name, ct);
    }

    /// <summary>
    /// Searches spells by name or words, returning the full listing filtered.
    /// </summary>
    public async Task<List<Spell>> SearchSpellsAsync(
        string? search = null,
        string? group = null,
        string? vocation = null,
        CancellationToken ct = default)
    {
        var all = await GetListingAsync(ct);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLowerInvariant();
            all = all.Where(s =>
                s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                SanitizeText(s.Words.ToLowerInvariant()).Contains(searchLower)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(group))
            all = all.Where(s =>
                s.Group != null &&
                s.Group.Equals(group, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(vocation))
            all = all.Where(s =>
                s.Vocation != null &&
                s.Vocation.Equals(vocation, StringComparison.OrdinalIgnoreCase)).ToList();

        return all;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Cloudflare bypass (cookie acquisition)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures we have a valid Cloudflare <c>__cf_bm</c> cookie cached.
    /// </summary>
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

    /// <summary>
    /// Fetches a wiki page via the MediaWiki API (<c>action=parse</c>).
    /// </summary>
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
    //  Listing page parsing
    // ──────────────────────────────────────────────────────────────────────

    private static List<Spell> ParseListingPage(HtmlDocument doc)
    {
        var results = new List<Spell>();

        var parser = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'mw-parser-output')]");
        if (parser == null) return results;

        // The Spells page has two wikitable sortable tables:
        //   Table 1 = Spells (instant spells)
        //   Table 2 = Runes
        // We only parse Table 1 (instant spells).
        var tables = parser.SelectNodes(".//table[contains(@class,'wikitable') and contains(@class,'sortable')]");
        if (tables == null || tables.Count == 0) return results;

        var spellTable = tables[0];
        var rows = spellTable.SelectNodes(".//tr");
        if (rows == null || rows.Count < 2) return results;

        foreach (var row in rows.Skip(1))
        {
            var cells = row.SelectNodes("./td");
            if (cells == null || cells.Count < 8) continue;

            // --- Name (column 0) ---
            var nameLink = cells[0].SelectSingleNode(".//a");
            var name = WebUtility.HtmlDecode(nameLink?.InnerText.Trim() ?? cells[0].InnerText.Trim());
            var href = nameLink?.GetAttributeValue("href", "");
            var fullUrl = string.IsNullOrWhiteSpace(href)
                ? $"{WikiBaseUrl}/wiki/{Uri.EscapeDataString(name)}"
                : $"{WikiBaseUrl}{href}";

            // --- Words (column 2) ---
            var words = WebUtility.HtmlDecode(cells[2].InnerText.Trim());

            // --- Premium (column 3) ---
            var premium = cells[3].InnerText.Contains("✓", StringComparison.Ordinal);

            // --- Level (column 4) ---
            var level = ParseInt(cells[4].InnerText.Trim());

            // --- Mana (column 5) ---
            var mana = ParseInt(cells[5].InnerText.Trim());

            // --- Group (column 6) ---
            var group = WebUtility.HtmlDecode(cells[6].InnerText.Trim());
            if (string.IsNullOrWhiteSpace(group)) group = null;

            // --- Effect (column 7) ---
            var effect = WebUtility.HtmlDecode(cells[7].InnerText.Trim());
            if (string.IsNullOrWhiteSpace(effect)) effect = null;

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(words))
            {
                results.Add(new Spell
                {
                    Name = SanitizeText(name),
                    Words = SanitizeText(words),
                    Url = fullUrl,
                    Premium = premium,
                    Level = level,
                    Mana = mana,
                    Group = group,
                    Effect = effect
                });
            }
        }

        return results;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Detail page parsing
    // ──────────────────────────────────────────────────────────────────────

    private static Spell ParseDetailPage(HtmlDocument doc, string name, string pageName, string url)
    {
        var parser = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'mw-parser-output')]");

        // Extract infobox values first, then build the immutable Spell in one shot.
        string? words = null;
        string? magicType = null;
        int? mana = null;
        string? group = null;
        string? cooldown = null;
        string? vocation = null;
        bool premium = false;
        int? level = null;
        int? basePower = null;
        string? version = null;
        string? status = null;

        if (parser != null)
        {
            var aside = parser.SelectSingleNode(".//aside");
            if (aside != null)
            {
                words = GetInfoBoxValue(aside, "words");
                magicType = GetInfoBoxValue(aside, "damagetype");
                mana = ParseInt(GetInfoBoxValue(aside, "mana"));
                group = GetInfoBoxValue(aside, "subclass");
                cooldown = GetInfoBoxValue(aside, "cooldown");
                vocation = GetInfoBoxValue(aside, "voc");

                var premiumStr = GetInfoBoxValue(aside, "premium");
                premium = premiumStr != null && premiumStr.Contains("✓", StringComparison.Ordinal);

                level = ParseInt(GetInfoBoxValue(aside, "levelrequired"));
                basePower = ParseInt(GetInfoBoxValue(aside, "basepower"));
                version = GetInfoBoxValue(aside, "implemented");
                status = GetInfoBoxValue(aside, "status");
            }
        }

        var detailedDescription = ExtractIntroParagraph(doc);
        var rawSections = ExtractSections(doc);

        return new Spell
        {
            Name = name,
            Words = words ?? string.Empty,
            Url = url,
            Premium = premium,
            Level = level,
            Mana = mana,
            Group = group,
            MagicType = magicType,
            Effect = null,
            Cooldown = cooldown,
            Vocation = vocation,
            BasePower = basePower,
            Version = version,
            Status = status,
            DetailedDescription = detailedDescription,
            Sections = rawSections
                .Select((s, i) => new SpellSection
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
    //  Intro / Sections (shared with conditions)
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
        // Remove non-numeric characters (e.g. "300\n" -> "300")
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
