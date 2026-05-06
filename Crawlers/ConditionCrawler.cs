using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace TibiaMcp.Server.Crawlers;

/// <summary>
/// Crawls the Tibia Wiki to extract special condition data.
///
/// Step 1 – List: fetches <c>/wiki/Special_Conditions</c> and parses the
///   wikitable to get condition name, type, and effect description.
/// Step 2 – Detail: for each condition, fetches the individual page
///   (e.g., <c>/wiki/Agony</c>) and extracts its sections.
/// </summary>
public partial class ConditionCrawler : CrawlerBase
{
    /// <summary>
    /// Represents a condition extracted from the listing table,
    /// before the detail page is crawled.
    /// </summary>
    public record ConditionListing(
        string Name,
        string WikiPageName,
        string Url,
        string Type,
        string EffectDescription
    );

    public ConditionCrawler(HttpClient httpClient, ILogger<ConditionCrawler> logger)
        : base(httpClient, logger)
    {
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Step 1 – List
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Crawls the Special Conditions listing page and returns all conditions found.
    /// </summary>
    public async Task<List<ConditionListing>> CrawlListingAsync(CancellationToken ct = default)
    {
        var url = $"{WikiBaseUrl}/wiki/Special_Conditions";
        var doc = await FetchPageAsync(url, ct);
        if (doc == null)
            return [];

        return ParseListingPage(doc);
    }

    /// <summary>
    /// Parses the Special Conditions wikitable.
    /// Columns: Name (with link), Type, Effect.
    /// </summary>
    internal static List<ConditionListing> ParseListingPage(HtmlDocument doc)
    {
        var results = new List<ConditionListing>();

        // Find the first wikitable.sortable inside mw-parser-output
        var parser = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'mw-parser-output')]");
        var table = parser?.SelectSingleNode(".//table[contains(@class,'wikitable') and contains(@class,'sortable')]");
        if (table == null) return results;

        var rows = table.SelectNodes(".//tr");
        if (rows == null || rows.Count < 2) return results;

        foreach (var row in rows.Skip(1)) // skip header row
        {
            var cells = row.SelectNodes("./td");
            if (cells == null || cells.Count < 3) continue;

            // --- Name (column 1) ---
            var nameCell = cells[0];

            // The cell contains an icon link (<a><img/></a>) and a text link (<a>Name</a>).
            // Find the anchor that carries the actual text (the one with non-empty trimmed text).
            var links = nameCell.SelectNodes(".//a");
            var link = links?.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.InnerText));
            link ??= links?.FirstOrDefault(); // fallback to any anchor

            var name = WebUtility.HtmlDecode(link?.InnerText.Trim() ?? nameCell.InnerText.Trim());
            var href = link?.GetAttributeValue("href", "");
            var wikiPageName = "";
            if (!string.IsNullOrWhiteSpace(href))
            {
                // Extract page name from href like "/wiki/Agony"
                wikiPageName = href.TrimStart('/');
                if (wikiPageName.StartsWith("wiki/", StringComparison.OrdinalIgnoreCase))
                    wikiPageName = wikiPageName[5..];
                // URL-decode
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
                results.Add(new ConditionListing(
                    Name: SanitizeText(name),
                    WikiPageName: SanitizeText(wikiPageName),
                    Url: fullUrl,
                    Type: SanitizeText(type),
                    EffectDescription: SanitizeText(effect)
                ));
            }
        }

        return results;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Step 2 – Detail
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Result of crawling a single condition detail page.
    /// </summary>
    public record ConditionDetail(
        string? DetailedDescription,
        List<ConditionSectionData> Sections
    );

    /// <summary>
    /// Data for a single section on a condition detail page.
    /// </summary>
    public record ConditionSectionData(
        string Heading,
        string? HeadingId,
        string Content
    );

    /// <summary>
    /// Crawls an individual condition page and extracts its sections.
    /// </summary>
    /// <param name="url">Full URL to the condition page.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ConditionDetail?> CrawlDetailAsync(string url, CancellationToken ct = default)
    {
        var doc = await FetchPageAsync(url, ct);
        if (doc == null) return null;

        var intro = ExtractIntroParagraph(doc);
        var rawSections = ExtractSections(doc);

        var sections = rawSections
            .Select((s, i) => new ConditionSectionData(
                Heading: SanitizeText(s.Heading),
                HeadingId: s.HeadingId,
                Content: SanitizeText(s.Content)
            ))
            .ToList();

        return new ConditionDetail(
            DetailedDescription: intro != null ? SanitizeText(intro) : null,
            Sections: sections
        );
    }

    /// <summary>
    /// Crawls the Special Conditions listing and then crawls every detail page.
    /// Returns fully populated condition data.
    /// </summary>
    public async Task<List<CrawledCondition>> CrawlAllAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var conditions = new List<CrawledCondition>();

        Logger.LogInformation("Step 1: Crawling Special Conditions listing...");
        progress?.Report("Crawling Special Conditions listing...");
        var listings = await CrawlListingAsync(ct);

        Logger.LogInformation("Found {Count} conditions in listing.", listings.Count);
        progress?.Report($"Found {listings.Count} conditions.");

        var delayBetweenRequests = TimeSpan.FromMilliseconds(500);

        foreach (var listing in listings)
        {
            if (ct.IsCancellationRequested) break;

            progress?.Report($"Crawling detail: {listing.Name}...");
            Logger.LogInformation("Crawling detail for {Name} at {Url}", listing.Name, listing.Url);

            // Be polite – short delay between requests
            await Task.Delay(delayBetweenRequests, ct);

            var detail = await CrawlDetailAsync(listing.Url, ct);

            conditions.Add(new CrawledCondition
            {
                Name = listing.Name,
                WikiPageName = listing.WikiPageName,
                Url = listing.Url,
                Type = listing.Type,
                EffectDescription = listing.EffectDescription,
                DetailedDescription = detail?.DetailedDescription,
                IsDetailCrawled = detail != null,
                Sections = detail?.Sections ?? []
            });
        }

        Logger.LogInformation("Crawled {Count} conditions total.", conditions.Count);
        progress?.Report($"Done. Crawled {conditions.Count} conditions.");

        return conditions;
    }

    /// <summary>
    /// Normalizes whitespace in extracted text.
    /// </summary>
    private static string SanitizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        // Collapse multiple whitespace / newlines into single spaces
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    /// Full crawled data for one condition, ready for persistence.
    /// </summary>
    public class CrawledCondition
    {
        public string Name { get; init; } = string.Empty;
        public string WikiPageName { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string EffectDescription { get; init; } = string.Empty;
        public string? DetailedDescription { get; init; }
        public bool IsDetailCrawled { get; init; }
        public List<ConditionSectionData> Sections { get; init; } = [];
    }
}
