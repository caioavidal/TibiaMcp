namespace TibiaMcp.Server.Models;

/// <summary>
/// A charm from the Tibia wiki (e.g., Adrenaline Burst, Bless, Carnage).
/// </summary>
public class Charm
{
    /// <summary>The name of the charm.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Fully-qualified URL to the charm's wiki page.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Charm type: Minor or Major.</summary>
    public string? Type { get; init; }

    /// <summary>Short effect description from the listing table.</summary>
    public string? Effect { get; init; }

    /// <summary>Charm Point cost (e.g., "100 / 150 / 225").</summary>
    public string? Cost { get; init; }

    /// <summary>Version when the charm was introduced.</summary>
    public string? Version { get; init; }

    /// <summary>Status (e.g., "Active").</summary>
    public string? Status { get; init; }

    /// <summary>Detailed description / introductory paragraph from the charm page.</summary>
    public string? DetailedDescription { get; init; }

    /// <summary>Sections extracted from the charm detail page.</summary>
    public List<CharmSection> Sections { get; init; } = [];
}
