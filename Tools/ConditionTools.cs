using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using TibiaMcp.Server.Crawlers;
using TibiaMcp.Server.Data;
using TibiaMcp.Server.Models;

namespace TibiaMcp.Server.Tools;

/// <summary>
/// MCP tools for querying and managing Special Conditions data.
/// </summary>
[McpServerToolType]
public class ConditionTools
{
    private readonly AppDbContext _db;
    private readonly ConditionCrawler _crawler;
    private readonly CrawlerRunner _runner;
    private readonly ILogger<ConditionTools> _logger;

    public ConditionTools(
        AppDbContext db,
        ConditionCrawler crawler,
        CrawlerRunner runner,
        ILogger<ConditionTools> logger)
    {
        _db = db;
        _crawler = crawler;
        _runner = runner;
        _logger = logger;
    }

    /// <summary>
    /// Gets all conditions from the database with optional filters.
    /// </summary>
    /// <param name="type">Optional filter by condition type (e.g., Harmful, Positive, Negative, Neutral).</param>
    /// <param name="search">Optional text search on condition name.</param>
    [McpServerTool]
    public async Task<List<Condition>> GetConditions(string? type = null, string? search = null)
    {
        var query = _db.Conditions
            .Include(c => c.Sections.OrderBy(s => s.SortOrder))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(c => c.Type == type);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => EF.Functions.ILike(c.Name, $"%{search}%"));

        return await query.OrderBy(c => c.Name).ToListAsync();
    }

    /// <summary>
    /// Gets a single condition by ID with all its sections.
    /// </summary>
    /// <param name="id">The condition ID.</param>
    [McpServerTool]
    public async Task<Condition?> GetConditionById(int id)
    {
        return await _db.Conditions
            .Include(c => c.Sections.OrderBy(s => s.SortOrder))
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>
    /// Gets a single condition by name.
    /// </summary>
    /// <param name="name">The condition name.</param>
    [McpServerTool]
    public async Task<Condition?> GetConditionByName(string name)
    {
        return await _db.Conditions
            .Include(c => c.Sections.OrderBy(s => s.SortOrder))
            .FirstOrDefaultAsync(c => c.Name == name);
    }

    /// <summary>
    /// Runs the condition crawler: fetches the Special Conditions listing
    /// and all detail pages, then saves/updates the database.
    /// </summary>
    [McpServerTool]
    public async Task<string> RunConditionCrawler()
    {
        // Use a simple progress collector
        var messages = new List<string>();

        var result = await _runner.RunAsync(
            progress: new Progress<string>(msg =>
            {
                messages.Add(msg);
                _logger.LogInformation("Crawl progress: {Msg}", msg);
            })
        );

        if (result.Success)
        {
            return $"✅ Crawl completed successfully!\n" +
                   $"   Conditions: {result.TotalConditions}\n" +
                   $"   Sections:   {result.TotalSections}\n" +
                   $"   Duration:   {result.Duration.TotalSeconds:F1}s";
        }

        return $"❌ Crawl completed with errors.\n" +
               $"   Conditions: {result.TotalConditions}\n" +
               $"   Sections:   {result.TotalSections}\n" +
               $"   Errors:     {result.Errors}\n" +
               $"   Duration:   {result.Duration.TotalSeconds:F1}s";
    }
}
