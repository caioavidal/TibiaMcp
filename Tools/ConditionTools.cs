using ModelContextProtocol.Server;
using TibiaMcp.Server.Models;
using TibiaMcp.Server.Services;

namespace TibiaMcp.Server.Tools;

/// <summary>
/// MCP tools for querying Tibia Special Conditions from the wiki in real time.
/// </summary>
[McpServerToolType]
public class ConditionTools
{
    private readonly ConditionWikiService _wiki;
    private readonly ILogger<ConditionTools> _logger;

    public ConditionTools(ConditionWikiService wiki, ILogger<ConditionTools> logger)
    {
        _wiki = wiki;
        _logger = logger;
    }

    /// <summary>
    /// Lists all special conditions from the Tibia wiki, with optional filters.
    /// </summary>
    /// <param name="type">Filter by condition type (e.g., Harmful, Positive, Negative, Neutral).</param>
    /// <param name="search">Search by condition name.</param>
    [McpServerTool]
    public async Task<List<Condition>> GetConditions(string? type = null, string? search = null)
    {
        var conditions = await _wiki.SearchConditionsAsync(type, search);

        _logger.LogInformation(
            "Returned {Count} conditions (type={Type}, search={Search})",
            conditions.Count, type ?? "*", search ?? "*");

        return conditions;
    }

    /// <summary>
    /// Gets a single condition by name, including its detailed description and sections.
    /// </summary>
    /// <param name="name">The exact condition name (e.g., "Agony", "Bleeding").</param>
    [McpServerTool]
    public async Task<Condition?> GetConditionByName(string name)
    {
        var condition = await _wiki.GetConditionAsync(name);

        if (condition == null)
            _logger.LogWarning("Condition '{Name}' not found on wiki.", name);
        else
            _logger.LogInformation("Returned condition '{Name}' with {Sections} sections.", name, condition.Sections.Count);

        return condition;
    }
}
