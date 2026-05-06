using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using TibiaMcp.Server.Models;

namespace TibiaMcp.Server.Services;

/// <summary>
/// Fetches condition data from the Tibia Fandom Wiki in real time.
/// </summary>
public partial class ConditionWikiService
{
    private const string WikiBaseUrl = "https://tibia.fandom.com";
    private readonly HttpClient _httpClient;
    private readonly ILogger<ConditionWikiService> _logger;

    public ConditionWikiService(HttpClient httpClient, ILogger<ConditionWikiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the Special Conditions listing from the wiki.
    /// </summary>
    public async Task<List<Condition>> GetListingAsync(CancellationToken ct = default)
    {
        var doc = await FetchPageAsync($"{WikiBaseUrl}/wiki/Special_Conditions", ct);
        if (doc == null) return [];

        return ParseListingPage(doc);
    }

    /// <summary>
    /// Fetches a single condition by name and returns it with its sections populated.
    /// </summary>
    public async Task<Condition?> GetConditionAsync(string name, CancellationToken ct = default)
    {
        var pageName = name.Replace(' ', '_');
        var url = $"{WikiBaseUrl}/wiki/{Uri.EscapeDataString(pageName)}";

        var doc = await FetchPageAsync(url, ct);
        if (doc == null) return null;

        var intro = ExtractIntroParagraph(doc);
        var rawSections = ExtractSections(doc);

        return new Condition
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
    //  HTTP / HTML helpers
    // ──────────────────────────────────────────────────────────────────────

    private async Task<HtmlDocument?> FetchPageAsync(string url, CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Fetching page: {Url}", url);

            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return doc;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching {Url}", url);
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Request timed out for {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching {Url}", url);
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
