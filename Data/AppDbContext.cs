using Microsoft.EntityFrameworkCore;
using TibiaMcp.Server.Models;

namespace TibiaMcp.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<GuildMember> GuildMembers => Set<GuildMember>();
    public DbSet<Condition> Conditions => Set<Condition>();
    public DbSet<ConditionSection> ConditionSections => Set<ConditionSection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasIndex(p => p.Name).IsUnique();
            entity.HasIndex(p => p.IsOnline);
            entity.HasIndex(p => p.Vocation);
        });

        modelBuilder.Entity<Guild>(entity =>
        {
            entity.HasIndex(g => g.Name).IsUnique();
        });

        modelBuilder.Entity<GuildMember>(entity =>
        {
            entity.HasIndex(gm => new { gm.GuildId, gm.PlayerId }).IsUnique();
        });

        modelBuilder.Entity<Condition>(entity =>
        {
            entity.HasIndex(c => c.Name).IsUnique();
            entity.HasIndex(c => c.WikiPageName);
            entity.HasIndex(c => c.Type);
        });

        modelBuilder.Entity<ConditionSection>(entity =>
        {
            entity.HasIndex(cs => cs.ConditionId);
            entity.HasIndex(cs => new { cs.ConditionId, cs.SortOrder });
        });
    }
}
