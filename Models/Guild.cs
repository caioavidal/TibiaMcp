using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TibiaMcp.Server.Models;

[Table("guilds")]
public class Guild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("owner_id")]
    public int? OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public Player? Owner { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GuildMember> Members { get; set; } = new List<GuildMember>();
}
