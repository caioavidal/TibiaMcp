using ModelContextProtocol.Server;
using TibiaMcp.Server.Models;
using TibiaMcp.Server.Services;

namespace TibiaMcp.Server.Tools;

/// <summary>
/// MCP tools for querying Tibia spells from the wiki in real time.
/// </summary>
[McpServerToolType]
public class SpellTools
{
    private readonly SpellWikiService _wiki;
    private readonly ILogger<SpellTools> _logger;

    public SpellTools(SpellWikiService wiki, ILogger<SpellTools> logger)
    {
        _wiki = wiki;
        _logger = logger;
    }

    /// <summary>
    /// Lists all instant spells from the Tibia wiki, with optional filters.
    /// </summary>
    /// <param name="search">Search by spell name or words.</param>
    /// <param name="group">Filter by spell group (e.g., Attack, Healing, Support).</param>
    /// <param name="vocation">Filter by vocation (e.g., Knight, Sorcerer, Druid, Paladin).</param>
    [McpServerTool]
    public async Task<List<Spell>> GetSpells(string? search = null, string? group = null, string? vocation = null)
    {
        var spells = await _wiki.SearchSpellsAsync(search, group, vocation);

        _logger.LogInformation(
            "Returned {Count} spells (search={Search}, group={Group}, vocation={Vocation})",
            spells.Count, search ?? "*", group ?? "*", vocation ?? "*");

        return spells;
    }

    /// <summary>
    /// Gets a single spell by name, including its detailed description and sections.
    /// </summary>
    /// <param name="name">The exact spell name (e.g., "Annihilation", "Haste").</param>
    [McpServerTool]
    public async Task<Spell?> GetSpellByName(string name)
    {
        var spell = await _wiki.GetSpellByNameAsync(name);

        if (spell == null)
            _logger.LogWarning("Spell '{Name}' not found on wiki", name);
        else
            _logger.LogInformation("Returned spell '{Name}' with {Sections} sections", name, spell.Sections.Count);

        return spell;
    }

    /// <summary>
    /// Gets a single spell by its magic words (e.g., "exori gran ico", "utamo vita").
    /// </summary>
    /// <param name="words">The magic words/phrase used to cast the spell.</param>
    [McpServerTool]
    public async Task<Spell?> GetSpellByWords(string words)
    {
        var spell = await _wiki.GetSpellByWordsAsync(words);

        if (spell == null)
            _logger.LogWarning("Spell with words '{Words}' not found on wiki", words);
        else
            _logger.LogInformation("Returned spell '{Name}' by words '{Words}'", spell.Name, words);

        return spell;
    }
}
