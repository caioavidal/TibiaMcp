namespace TibiaMcp.Server.Models;

/// <summary>
/// A special condition from the Tibia wiki (e.g., Agony, Bleeding, Burning).
/// </summary>
public class Condition
{
    /// <summary>The name of the condition (e.g., "Agony", "Bleeding").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The wiki page name (may differ from Name due to URL encoding or redirects).</summary>
    public string WikiPageName { get; init; } = string.Empty;

    /// <summary>Fully-qualified URL to the condition page.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>The type category (e.g., "Harmful", "Negative", "Positive", "Neutral", "Mixed", "Taints").</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Short effect description from the listing table.</summary>
    public string EffectDescription { get; init; } = string.Empty;

    /// <summary>Detailed description / introductory paragraph from the condition page.</summary>
    public string? DetailedDescription { get; init; }

    /// <summary>Sections extracted from the condition detail page.</summary>
    public List<ConditionSection> Sections { get; init; } = [];
}
