using ModelContextProtocol.Server;
using TibiaMcp.Server.Models;
using TibiaMcp.Server.Services;

namespace TibiaMcp.Server.Tools;

/// <summary>
/// MCP tools for querying Tibia creatures from the wiki in real time.
/// </summary>
[McpServerToolType]
public class CreatureTools
{
    private readonly CreatureWikiService _wiki;
    private readonly ILogger<CreatureTools> _logger;

    public CreatureTools(CreatureWikiService wiki, ILogger<CreatureTools> logger)
    {
        _wiki = wiki;
        _logger = logger;
    }

    /// <summary>
    /// Lists all creatures from the Tibia wiki (List_of_Creatures), with optional name search.
    /// Returns name, HP, exp, version, and wiki URL for every creature.
    /// </summary>
    /// <param name="search">Search by creature name substring.</param>
    [McpServerTool]
    public async Task<List<Creature>> GetCreatures(string? search = null)
    {
        var creatures = await _wiki.SearchCreaturesAsync(search);

        _logger.LogInformation(
            "Returned {Count} creatures (search={Search})",
            creatures.Count, search ?? "*");

        return creatures;
    }

    /// <summary>
    /// Gets a single creature by name, including full infobox data (hp, exp, speed, classification, etc.) and sections.
    /// </summary>
    /// <param name="name">The exact creature name (e.g., "A Greedy Eye", "Demon", "Dragon").</param>
    [McpServerTool]
    public async Task<Creature?> GetCreatureByName(string name)
    {
        var creature = await _wiki.GetCreatureByNameAsync(name);

        if (creature == null)
            _logger.LogWarning("Creature '{Name}' not found on wiki.", name);
        else
            _logger.LogInformation("Returned creature '{Name}' with {Sections} sections.", name, creature.Sections.Count);

        return creature;
    }


}
