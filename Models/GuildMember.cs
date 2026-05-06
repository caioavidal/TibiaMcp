using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TibiaMcp.Server.Models;

[Table("guild_members")]
public class GuildMember
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("guild_id")]
    public int GuildId { get; set; }

    [ForeignKey(nameof(GuildId))]
    public Guild Guild { get; set; } = null!;

    [Column("player_id")]
    public int PlayerId { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public Player Player { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    [Column("rank")]
    public string Rank { get; set; } = string.Empty;

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
