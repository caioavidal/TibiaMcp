using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TibiaMcp.Server.Models;

/// <summary>
/// Represents a named section within a special condition's detail page.
/// Example: "Getting in Agony", "Removing Agony", "How to get Electrified".
/// </summary>
[Table("condition_sections")]
public class ConditionSection
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Foreign key to the parent condition.</summary>
    [Column("condition_id")]
    public int ConditionId { get; set; }

    [ForeignKey(nameof(ConditionId))]
    public Condition Condition { get; set; } = null!;

    /// <summary>The section heading text (e.g., "Getting in Agony").</summary>
    [Required]
    [MaxLength(300)]
    [Column("heading")]
    public string Heading { get; set; } = string.Empty;

    /// <summary>The HTML id attribute of the section heading (for anchor links).</summary>
    [MaxLength(300)]
    [Column("heading_id")]
    public string? HeadingId { get; set; }

    /// <summary>The text content of the section.</summary>
    [Column("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>0-based ordering of sections on the page.</summary>
    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
