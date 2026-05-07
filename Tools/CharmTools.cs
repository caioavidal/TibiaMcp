using ModelContextProtocol.Server;
using TibiaMcp.Server.Models;
using TibiaMcp.Server.Services;

namespace TibiaMcp.Server.Tools;

/// <summary>
/// MCP tools for querying Tibia charms from the wiki in real time.
/// </summary>
[McpServerToolType]
public class CharmTools
{
    private readonly CharmWikiService _wiki;
    private readonly ILogger<CharmTools> _logger;

    public CharmTools(CharmWikiService wiki, ILogger<CharmTools> logger)
    {
        _wiki = wiki;
        _logger = logger;
    }

    /// <summary>
    /// Gets the charm feature overview from the Cyclopedia page.
    /// Returns introductory paragraphs explaining how the charms system works.
    /// </summary>
    [McpServerTool]
    public async Task<string?> GetCharmsFeatureInfo()
    {
        var feature = await _wiki.GetFeatureAsync();

        _logger.LogInformation(
            "Returned charm feature info (length={Length})",
            feature?.Length ?? 0);

        return feature;
    }

    /// <summary>
    /// Lists all charms from the wiki, with optional filters.
    /// Returns name, type, effect, and cost for each charm.
    /// </summary>
    /// <param name="search">Search by charm name substring.</param>
    /// <param name="type">Filter by charm type (Minor or Major).</param>
    [McpServerTool]
    public async Task<List<Charm>> GetCharms(string? search = null, string? type = null)
    {
        var charms = await _wiki.SearchCharmsAsync(search, type);

        _logger.LogInformation(
            "Returned {Count} charms (search={Search}, type={Type})",
            charms.Count, search ?? "*", type ?? "*");

        return charms;
    }

    /// <summary>
    /// Gets a single charm by name, including full infobox data and sections.
    /// </summary>
    /// <param name="name">The exact charm name (e.g., "Adrenaline Burst", "Bless", "Cripple").</param>
    [McpServerTool]
    public async Task<Charm?> GetCharmByName(string name)
    {
        var charm = await _wiki.GetCharmByNameAsync(name);

        if (charm == null)
            _logger.LogWarning("Charm '{Name}' not found on wiki.", name);
        else
            _logger.LogInformation("Returned charm '{Name}' with {Sections} sections.", name, charm.Sections.Count);

        return charm;
    }
}
