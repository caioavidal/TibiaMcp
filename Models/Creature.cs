namespace TibiaMcp.Server.Models;

/// <summary>
/// A creature from the Tibia wiki (e.g., A Greedy Eye, Demon, Dragon).
/// </summary>
public class Creature
{
    /// <summary>The name of the creature.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Fully-qualified URL to the creature's wiki page.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Health points (from infobox or classification table).</summary>
    public string? Hp { get; init; }

    /// <summary>Experience points awarded (from infobox or classification table).</summary>
    public string? Exp { get; init; }

    /// <summary>Classification/creature type (e.g., "Ghosts", "Demons", "Humans").</summary>
    public string? Classification { get; init; }

    /// <summary>Creature speed.</summary>
    public int? Speed { get; init; }

    /// <summary>Estimated max damage.</summary>
    public string? MaxDamage { get; init; }

    /// <summary>Summon information.</summary>
    public string? Summon { get; init; }

    /// <summary>Convince information.</summary>
    public string? Convince { get; init; }

    /// <summary>Spawn type (e.g., "Unblockable").</summary>
    public string? SpawnType { get; init; }

    /// <summary>Whether the creature is illusionable.</summary>
    public bool? Illusionable { get; init; }

    /// <summary>Whether the creature is pushable.</summary>
    public bool? Pushable { get; init; }

    /// <summary>Whether the creature pushes objects.</summary>
    public bool? Pushes { get; init; }

    /// <summary>Whether the creature is paralysable.</summary>
    public string? Paralysable { get; init; }

    /// <summary>Whether the creature senses invisibility.</summary>
    public bool? SenseInvisibility { get; init; }

    /// <summary>What the creature walks through (e.g., "Energy Fire Poison").</summary>
    public string? WalksThrough { get; init; }

    /// <summary>Version when the creature was introduced.</summary>
    public string? Version { get; init; }

    /// <summary>Status (e.g., "Active").</summary>
    public string? Status { get; init; }

    /// <summary>Loot description (from classification table).</summary>
    public string? Loot { get; init; }

    /// <summary>Detailed description / introductory paragraph from the creature page.</summary>
    public string? DetailedDescription { get; init; }

    /// <summary>Sections extracted from the creature detail page.</summary>
    public List<CreatureSection> Sections { get; init; } = [];
}
