namespace TibiaMcp.Server.Models;

/// <summary>
/// A named section within a charm's detail page.
/// Example: "Effect", "Notes", "Related Charms".
/// </summary>
public class CharmSection
{
    /// <summary>The section heading text.</summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>The HTML id attribute of the section heading (for anchor links).</summary>
    public string? HeadingId { get; init; }

    /// <summary>The text content of the section.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>0-based ordering of sections on the page.</summary>
    public int SortOrder { get; init; }
}
