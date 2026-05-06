using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TibiaMcp.Server.Models;

/// <summary>
/// Represents a special condition from the Tibia wiki (e.g., Agony, Bleeding, Burning).
/// </summary>
[Table("conditions")]
public class Condition
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>The name of the condition (e.g., "Agony", "Bleeding").</summary>
    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>The wiki page name (may differ from Name due to URL encoding or redirects).</summary>
    [MaxLength(200)]
    [Column("wiki_page_name")]
    public string WikiPageName { get; set; } = string.Empty;

    /// <summary>Fully-qualified URL to the condition page.</summary>
    [MaxLength(500)]
    [Column("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>The type category (e.g., "Harmful", "Negative", "Positive", "Neutral", "Mixed", "Taints").</summary>
    [Required]
    [MaxLength(50)]
    [Column("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Short effect description from the listing table.</summary>
    [Column("effect_description")]
    public string EffectDescription { get; set; } = string.Empty;

    /// <summary>Detailed description / introductory paragraph from the condition page.</summary>
    [Column("detailed_description")]
    public string? DetailedDescription { get; set; }

    /// <summary>Whether the condition page has been fully crawled for sections.</summary>
    [Column("is_detail_crawled")]
    public bool IsDetailCrawled { get; set; }

    [Column("crawled_at")]
    public DateTime? CrawledAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Sections extracted from the condition detail page.</summary>
    public ICollection<ConditionSection> Sections { get; set; } = new List<ConditionSection>();
}
