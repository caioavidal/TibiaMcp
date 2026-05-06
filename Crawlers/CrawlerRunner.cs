using Microsoft.EntityFrameworkCore;
using TibiaMcp.Server.Data;
using TibiaMcp.Server.Models;

namespace TibiaMcp.Server.Crawlers;

/// <summary>
/// Orchestrates crawling and saving data to the database.
/// Can be triggered via MCP tool or console command.
/// </summary>
public class CrawlerRunner
{
    private readonly ConditionCrawler _crawler;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CrawlerRunner> _logger;

    public CrawlerRunner(
        ConditionCrawler crawler,
        IServiceScopeFactory scopeFactory,
        ILogger<CrawlerRunner> logger)
    {
        _crawler = crawler;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Runs the full crawl: fetches the listing, then each detail page,
    /// and saves/updates everything into the database.
    /// </summary>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<CrawlResult> RunAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        var totalConditions = 0;
        var totalSections = 0;
        var errors = 0;

        try
        {
            // ── Crawl ────────────────────────────────────────────────────
            var crawled = await _crawler.CrawlAllAsync(progress, ct);

            totalConditions = crawled.Count;
            totalSections = crawled.Sum(c => c.Sections.Count);

            // ── Persist ──────────────────────────────────────────────────
            progress?.Report("Saving data to database...");
            await SaveConditionsAsync(crawled, ct);

            progress?.Report("Crawl completed successfully.");
            _logger.LogInformation(
                "Crawl finished. Conditions: {Conditions}, Sections: {Sections}",
                totalConditions, totalSections);
        }
        catch (OperationCanceledException)
        {
            progress?.Report("Crawl was cancelled.");
            _logger.LogWarning("Crawl cancelled.");
        }
        catch (Exception ex)
        {
            errors++;
            progress?.Report($"Crawl failed: {ex.Message}");
            _logger.LogError(ex, "Crawl failed.");
        }

        return new CrawlResult
        {
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            TotalConditions = totalConditions,
            TotalSections = totalSections,
            Errors = errors
        };
    }

    /// <summary>
    /// Persists the crawled conditions using a fresh DbContext scope.
    /// Uses upsert logic: updates existing records, inserts new ones.
    /// </summary>
    private async Task SaveConditionsAsync(
        List<ConditionCrawler.CrawledCondition> crawled,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var crawledCondition in crawled)
        {
            // Look for an existing condition by wiki page name (or by name as fallback)
            var existing = await db.Conditions
                .Include(c => c.Sections)
                .FirstOrDefaultAsync(
                    c => c.WikiPageName == crawledCondition.WikiPageName
                         || (string.IsNullOrEmpty(crawledCondition.WikiPageName) && c.Name == crawledCondition.Name),
                    ct);

            if (existing != null)
            {
                // Update
                existing.Name = crawledCondition.Name;
                existing.WikiPageName = crawledCondition.WikiPageName;
                existing.Url = crawledCondition.Url;
                existing.Type = crawledCondition.Type;
                existing.EffectDescription = crawledCondition.EffectDescription;
                existing.DetailedDescription = crawledCondition.DetailedDescription ?? existing.DetailedDescription;
                existing.IsDetailCrawled = crawledCondition.IsDetailCrawled;
                existing.CrawledAt = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;

                // Replace sections
                db.ConditionSections.RemoveRange(existing.Sections);

                foreach (var s in crawledCondition.Sections)
                {
                    existing.Sections.Add(new ConditionSection
                    {
                        Heading = s.Heading,
                        HeadingId = s.HeadingId,
                        Content = s.Content,
                        SortOrder = existing.Sections.Count
                    });
                }
            }
            else
            {
                // Insert new
                var condition = new Condition
                {
                    Name = crawledCondition.Name,
                    WikiPageName = crawledCondition.WikiPageName,
                    Url = crawledCondition.Url,
                    Type = crawledCondition.Type,
                    EffectDescription = crawledCondition.EffectDescription,
                    DetailedDescription = crawledCondition.DetailedDescription,
                    IsDetailCrawled = crawledCondition.IsDetailCrawled,
                    CrawledAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                foreach (var s in crawledCondition.Sections)
                {
                    condition.Sections.Add(new ConditionSection
                    {
                        Heading = s.Heading,
                        HeadingId = s.HeadingId,
                        Content = s.Content,
                        SortOrder = condition.Sections.Count
                    });
                }

                db.Conditions.Add(condition);
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Saved {Count} conditions to database.", crawled.Count);
    }
}

/// <summary>
/// Result of a crawl run.
/// </summary>
public class CrawlResult
{
    public DateTime StartedAt { get; init; }
    public DateTime CompletedAt { get; init; }
    public int TotalConditions { get; init; }
    public int TotalSections { get; init; }
    public int Errors { get; init; }
    public TimeSpan Duration => CompletedAt - StartedAt;
    public bool Success => Errors == 0 && TotalConditions > 0;
}
