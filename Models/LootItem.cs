namespace TibiaMcp.Server.Models;

/// <summary>
/// An item in a creature's loot table.
/// </summary>
public class LootItem
{
    /// <summary>The name of the item.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Task requirement (if any).</summary>
    public string? Task { get; init; }

    /// <summary>Quantity range (e.g., "1-104", "1").</summary>
    public string? Quantity { get; init; }

    /// <summary>Average quantity.</summary>
    public string? Average { get; init; }

    /// <summary>Drop probability percentage (e.g., "90%").</summary>
    public string? Probability { get; init; }
}
