namespace TibiaMcp.Server.Models;

/// <summary>
/// A spell from the Tibia wiki (e.g., Annihilation, Haste, Great Light).
/// </summary>
public class Spell
{
    /// <summary>The name of the spell (e.g., "Annihilation").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The magic words required to cast this spell (e.g., "exori gran ico").</summary>
    public string Words { get; init; } = string.Empty;

    /// <summary>Fully-qualified URL to the spell's wiki page.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Whether a premium account is required.</summary>
    public bool Premium { get; init; }

    /// <summary>Minimum level required to cast the spell.</summary>
    public int? Level { get; init; }

    /// <summary>Mana cost of the spell.</summary>
    public int? Mana { get; init; }

    /// <summary>Spell group (e.g., "Attack", "Healing", "Support").</summary>
    public string? Group { get; init; }

    /// <summary>Magic type (e.g., "Physical Damage", "Fire Damage").</summary>
    public string? MagicType { get; init; }

    /// <summary>Short effect description from the listing table.</summary>
    public string? Effect { get; init; }

    /// <summary>Individual cooldown (e.g., "30 seconds").</summary>
    public string? Cooldown { get; init; }

    /// <summary>Vocation restriction (e.g., "Knight", "Sorcerer", "None").</summary>
    public string? Vocation { get; init; }

    /// <summary>Base power of the spell.</summary>
    public int? BasePower { get; init; }

    /// <summary>Version when the spell was introduced.</summary>
    public string? Version { get; init; }

    /// <summary>Status (e.g., "Active").</summary>
    public string? Status { get; init; }

    /// <summary>Detailed description / introductory paragraph from the spell page.</summary>
    public string? DetailedDescription { get; init; }

    /// <summary>Sections extracted from the spell detail page.</summary>
    public List<SpellSection> Sections { get; init; } = [];
}
