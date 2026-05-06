using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TibiaMcp.Server.Models;

[Table("players")]
public class Player
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("vocation")]
    public string Vocation { get; set; } = string.Empty;

    [Column("level")]
    public int Level { get; set; }

    [Column("experience")]
    public long Experience { get; set; }

    [Column("health")]
    public int Health { get; set; }

    [Column("mana")]
    public int Mana { get; set; }

    [MaxLength(100)]
    [Column("town")]
    public string? Town { get; set; }

    [Column("is_online")]
    public bool IsOnline { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
