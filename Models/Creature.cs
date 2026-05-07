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

    /// <summary>Health points.</summary>
    public string? Hp { get; init; }

    /// <summary>Experience points awarded.</summary>
    public string? Exp { get; init; }

    /// <summary>Classification/creature type (e.g., "Demons", "Dragons").</summary>
    public string? Classification { get; init; }

    // ── Combat Properties ──────────────────────────────────────────────

    /// <summary>Creature speed.</summary>
    public int? Speed { get; init; }

    /// <summary>Estimated max damage.</summary>
    public string? MaxDamage { get; init; }

    /// <summary>Armor rating.</summary>
    public int? Armor { get; init; }

    /// <summary>Damage mitigation percentage.</summary>
    public string? Mitigation { get; init; }

    /// <summary>Elements used (e.g., "Physical").</summary>
    public string? Elements { get; init; }

    /// <summary>Summon information.</summary>
    public string? Summon { get; init; }

    /// <summary>Convince information.</summary>
    public string? Convince { get; init; }

    // ── General Properties ─────────────────────────────────────────────

    /// <summary>Spawn type (e.g., "Regular", "Raid", "Unblockable").</summary>
    public string? SpawnType { get; init; }

    /// <summary>Whether the creature is a boss.</summary>
    public bool? IsBoss { get; init; }

    /// <summary>Whether the creature is illusionable.</summary>
    public bool? Illusionable { get; init; }

    /// <summary>Whether the creature is pushable.</summary>
    public bool? Pushable { get; init; }

    /// <summary>Whether the creature pushes objects.</summary>
    public bool? Pushes { get; init; }

    // ── Bestiary Properties ────────────────────────────────────────────

    /// <summary>Bestiary class (e.g., "Dragon", "Demon", "Mammal").</summary>
    public string? BestiaryClass { get; init; }

    /// <summary>Bestiary difficulty level.</summary>
    public string? BestiaryDifficulty { get; init; }

    /// <summary>Bestiary behaviour description.</summary>
    public string? Behaviour { get; init; }

    /// <summary>Charm points awarded for unlocking.</summary>
    public string? CharmPoints { get; init; }

    /// <summary>Kills required to unlock in the bestiary.</summary>
    public string? KillsToUnlock { get; init; }

    // ── Bosstiary Properties ───────────────────────────────────────────

    /// <summary>Bosstiary category (e.g., "Nemesis").</summary>
    public string? BosstiaryCategory { get; init; }

    // ── Immunity Properties ────────────────────────────────────────────

    /// <summary>Whether the creature is paralysable.</summary>
    public string? Paralysable { get; init; }

    /// <summary>Whether the creature senses invisibility.</summary>
    public bool? SenseInvisibility { get; init; }

    // ── Behavioural Properties ─────────────────────────────────────────

    /// <summary>Health threshold at which the creature runs away.</summary>
    public string? RunsAt { get; init; }

    /// <summary>What the creature walks around (e.g., "Energy Fire Poison").</summary>
    public string? WalksAround { get; init; }

    /// <summary>What the creature walks through (e.g., "Energy Fire Poison").</summary>
    public string? WalksThrough { get; init; }

    // ── Other Properties ───────────────────────────────────────────────

    /// <summary>Version when the creature was introduced.</summary>
    public string? Version { get; init; }

    /// <summary>Status (e.g., "Active").</summary>
    public string? Status { get; init; }

    // ── Detailed Info ──────────────────────────────────────────────────

    /// <summary>Loot table parsed from the creature's detail page.</summary>
    public List<LootItem>? Loot { get; init; }

    /// <summary>Detailed description / introductory paragraph from the creature page.</summary>
    public string? DetailedDescription { get; init; }

    /// <summary>Sections extracted from the creature detail page.</summary>
    public List<CreatureSection> Sections { get; init; } = [];
}
