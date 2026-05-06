using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using TibiaMcp.Server.Models;

namespace TibiaMcp.Server.Services;

/// <summary>
/// Fetches condition data from the Tibia Fandom Wiki in real time.
/// Results are cached in memory because the wiki content changes infrequently.
/// </summary>
public partial class ConditionWikiService
{
    private const string WikiBaseUrl = "https://tibia.fandom.com";
    private const string ApiBaseUrl = "https://tibia.fandom.com/api.php";

    // Cache keys
    private const string CacheKeyListing = "conditions:listing";
    private const string CacheKeyDetailPrefix = "conditions:detail:";

    // Cache duration – wiki content is stable, 2 hours is safe
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(2);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ConditionWikiService> _logger;

    // Cloudflare cookie management
    private DateTime _cookieExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _warmupLock = new(1, 1);

    public ConditionWikiService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<ConditionWikiService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the Special Conditions listing from the wiki.
    /// Results are cached in memory for <see cref="CacheDuration"/>.
    /// </summary>
    public async Task<List<Condition>> GetListingAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue<List<Condition>>(CacheKeyListing, out var cached))
            return cached!;

        var doc = await FetchPageViaApiAsync("Special_Conditions", ct);
        if (doc == null) return [];

        var results = ParseListingPage(doc);

        _cache.Set(CacheKeyListing, results, CacheDuration);
        _logger.LogDebug("Cached condition listing ({Count} items) for {Duration}",
            results.Count, CacheDuration);

        return results;
    }

    /// <summary>
    /// Fetches a single condition by name and returns it with its sections populated.
    /// Results are cached in memory for <see cref="CacheDuration"/>.
    /// </summary>
    public async Task<Condition?> GetConditionAsync(string name, CancellationToken ct = default)
    {
        var pageName = name.Replace(' ', '_');
        var cacheKey = $"{CacheKeyDetailPrefix}{pageName}";

        if (_cache.TryGetValue<Condition>(cacheKey, out var cached))
            return cached;

        var url = $"{WikiBaseUrl}/wiki/{Uri.EscapeDataString(pageName)}";

        var doc = await FetchPageViaApiAsync(pageName, ct);
        if (doc == null) return null;

        var intro = ExtractIntroParagraph(doc);
        var rawSections = ExtractSections(doc);

        var result = new Condition
        {
            Name = name,
            WikiPageName = pageName,
            Url = url,
            Type = "Unknown",   // we'd need the listing table to know the type
            EffectDescription = string.Empty,
            DetailedDescription = intro != null ? SanitizeText(intro) : null,
            Sections = rawSections
                .Select((s, i) => new ConditionSection
                {
                    Heading = SanitizeText(s.Heading),
                    HeadingId = s.HeadingId,
                    Content = SanitizeText(s.Content),
                    SortOrder = i
                })
                .ToList()
        };

        _cache.Set(cacheKey, result, CacheDuration);
        _logger.LogDebug("Cached condition '{Name}' for {Duration}", name, CacheDuration);

        return result;
    }

    /// <summary>
    /// Searches conditions by fetching the listing and filtering by name.
    /// </summary>
    public async Task<List<Condition>> SearchConditionsAsync(
        string? type = null,
        string? search = null,
        CancellationToken ct = default)
    {
        var all = await GetListingAsync(ct);

        if (!string.IsNullOrWhiteSpace(type))
            all = all.Where(c =>
                c.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(search))
            all = all.Where(c =>
                c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        return all;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Cloudflare bypass (cookie acquisition)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures we have a valid Cloudflare <c>__cf_bm</c> cookie cached.
    /// Fandom blocks raw .NET HTTP clients with a TLS fingerprint check and
    /// returns a 403 challenge.  However the challenge response <em>does</em>
    /// set the <c>__cf_bm</c> cookie.  We capture it and reuse it on the
    /// MediaWiki API endpoint, which accepts it.
    /// </summary>
    private async Task EnsureCookieAsync(CancellationToken ct)
    {
        // The cookie is valid for ~30 min; refresh when 5 min remain.
        // NB: _cookieExpiry starts as DateTime.MinValue, so we guard against
        //     arithmetic underflow by checking for the default first.
        if (_cookieExpiry != DateTime.MinValue &&
            DateTime.UtcNow < _cookieExpiry.AddMinutes(-5))
            return;

        await _warmupLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring the lock
            if (_cookieExpiry != DateTime.MinValue &&
                DateTime.UtcNow < _cookieExpiry.AddMinutes(-5))
                return;

            _logger.LogDebug("Refreshing Cloudflare cookie via warm-up request…");

            // Any GET to the Fandom domain sets the cookie in the 403 response.
            // We hit the base URL because it's lightweight.
            using var response = await _httpClient.GetAsync(WikiBaseUrl, ct);

            // Cloudflare always sends a new __cf_bm on challenge responses.
            // The CookieContainer inside the handler automatically captures it.
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
    /// The API is far more lenient than the main HTML endpoint and accepts
    /// requests that carry a valid <c>__cf_bm</c> cookie.
    /// </summary>
    /// <param name="pageName">Wiki page title (e.g. "Special_Conditions", "Haste").</param>
    private async Task<HtmlDocument?> FetchPageViaApiAsync(string pageName, CancellationToken ct)
    {
        // 1. Ensure we have a Cloudflare cookie before hitting the API.
        await EnsureCookieAsync(ct);

        // 2. Build the API URL.
        //    The MediaWiki parse API returns parsed HTML directly.
        var apiUrl = $"{ApiBaseUrl}?action=parse&page={Uri.EscapeDataString(pageName)}&format=json&prop=text&redirects=";

        try
        {
            _logger.LogDebug("Fetching wiki page via API: {PageName}", pageName);

            var response = await _httpClient.GetAsync(apiUrl, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var jsonDoc = JsonDocument.Parse(json);

            // Validate the response structure.
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

            // The API returns the inner content of the mw-parser-output div.
            // Wrap it in a structure that our existing XPath-based parsers expect.
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

    private static List<Condition> ParseListingPage(HtmlDocument doc)
    {
        var results = new List<Condition>();

        var parser = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'mw-parser-output')]");
        var table = parser?.SelectSingleNode(".//table[contains(@class,'wikitable') and contains(@class,'sortable')]");
        if (table == null) return results;

        var rows = table.SelectNodes(".//tr");
        if (rows == null || rows.Count < 2) return results;

        foreach (var row in rows.Skip(1))
        {
            var cells = row.SelectNodes("./td");
            if (cells == null || cells.Count < 3) continue;

            // --- Name (column 1) ---
            var nameCell = cells[0];
            var links = nameCell.SelectNodes(".//a");
            var link = links?.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.InnerText));
            link ??= links?.FirstOrDefault();

            var name = WebUtility.HtmlDecode(link?.InnerText.Trim() ?? nameCell.InnerText.Trim());
            var href = link?.GetAttributeValue("href", "");
            var wikiPageName = "";
            if (!string.IsNullOrWhiteSpace(href))
            {
                wikiPageName = href.TrimStart('/');
                if (wikiPageName.StartsWith("wiki/", StringComparison.OrdinalIgnoreCase))
                    wikiPageName = wikiPageName[5..];
                wikiPageName = WebUtility.UrlDecode(wikiPageName);
            }

            var fullUrl = string.IsNullOrWhiteSpace(href)
                ? $"{WikiBaseUrl}/wiki/{Uri.EscapeDataString(name)}"
                : $"{WikiBaseUrl}{href}";

            // --- Type (column 2) ---
            var type = WebUtility.HtmlDecode(cells[1].InnerText.Trim());

            // --- Effect (column 3) ---
            var effect = WebUtility.HtmlDecode(cells[2].InnerText.Trim());

            if (!string.IsNullOrWhiteSpace(name))
            {
                results.Add(new Condition
                {
                    Name = SanitizeText(name),
                    WikiPageName = SanitizeText(wikiPageName),
                    Url = fullUrl,
                    Type = SanitizeText(type),
                    EffectDescription = SanitizeText(effect)
                });
            }
        }

        return results;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Detail page parsing  (moved from old CrawlerBase)
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

    private static string SanitizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
