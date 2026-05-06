using System.Net;
using HtmlAgilityPack;

namespace TibiaMcp.Server.Crawlers;

/// <summary>
/// Abstract base class for Tibia Wiki crawlers.
/// Provides common HTTP fetching and HTML parsing infrastructure.
/// </summary>
public abstract class CrawlerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    protected CrawlerBase(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    protected ILogger Logger => _logger;

    /// <summary>
    /// The base URL of the Tibia Wiki.
    /// </summary>
    public const string WikiBaseUrl = "https://tibia.fandom.com";

    /// <summary>
    /// Fetches the HTML content of a wiki page as an <see cref="HtmlDocument"/>.
    /// Automatically respects a small delay to be polite to the server.
    /// </summary>
    protected async Task<HtmlDocument?> FetchPageAsync(string url, CancellationToken ct = default)
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

    /// <summary>
    /// Extracts the text content of the first paragraph inside the article's main content
    /// (i.e., the first <c>&lt;p&gt;</c> inside <c>.mw-parser-output</c> that contains meaningful text).
    /// </summary>
    internal static string? ExtractIntroParagraph(HtmlDocument doc)
    {
        var parser = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'mw-parser-output')]");
        if (parser == null) return null;

        // First, try to find the first <p> that isn't empty and isn't inside an infobox or navbox
        var paragraphs = parser.SelectNodes(".//p[@class != 'mw-empty-elt']");
        if (paragraphs != null)
        {
            foreach (var p in paragraphs)
            {
                // Skip <p> that is inside an infobox <aside> or navbox <table>
                if (p.Ancestors("aside").Any() || p.Ancestors("table").Any())
                    continue;

                var text = WebUtility.HtmlDecode(p.InnerText.Trim());
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 10)
                    return text;
            }
        }

        // Fallback: some pages put the intro directly after the infobox, not in a <p>.
        // Collect text from all inline nodes after the infobox until we hit a heading.
        var aside = parser.SelectSingleNode(".//aside");
        var startNode = aside?.NextSibling ?? parser.FirstChild;

        var introParts = new List<string>();
        while (startNode != null)
        {
            // Stop at headings, tables, or other structural elements
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
        if (combined.Length > 10)
            return combined;

        return null;
    }

    /// <summary>
    /// Extracts all h2/h3 sections from the article content.
    /// Returns tuples of (headingText, headingId, innerHtmlContent).
    /// Skips "See Also" and navbox-related sections.
    /// </summary>
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

            // Skip "See Also" and other meta sections
            if (string.IsNullOrWhiteSpace(headingText)) continue;
            if (headingText.Equals("See Also", StringComparison.OrdinalIgnoreCase)) continue;
            if (headingText.Equals("References", StringComparison.OrdinalIgnoreCase)) continue;
            if (headingText.Equals("External Links", StringComparison.OrdinalIgnoreCase)) continue;

            // Collect all content nodes between this heading and the next heading
            var contentParts = new List<string>();
            var next = headingNode.NextSibling;
            while (next != null && next.Name != "h2" && next.Name != "h3")
            {
                if (next.NodeType == HtmlNodeType.Element &&
                    next.Name != "table") // skip navbox tables
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
}
