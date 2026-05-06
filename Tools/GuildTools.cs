using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using TibiaMcp.Server.Data;
using TibiaMcp.Server.Models;

namespace TibiaMcp.Server.Tools;

/// <summary>
/// MCP tools for managing Tibia guilds.
/// </summary>
[McpServerToolType]
public class GuildTools
{
    private readonly AppDbContext _db;

    public GuildTools(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Gets all guilds.
    /// </summary>
    [McpServerTool]
    public async Task<List<Guild>> GetAllGuilds()
    {
        return await _db.Guilds
            .Include(g => g.Owner)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a guild by ID with its members.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    [McpServerTool]
    public async Task<Guild?> GetGuildById(int id)
    {
        return await _db.Guilds
            .Include(g => g.Owner)
            .Include(g => g.Members)
                .ThenInclude(m => m.Player)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    /// <summary>
    /// Creates a new guild.
    /// </summary>
    /// <param name="name">The guild name.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="ownerId">The player ID of the guild owner.</param>
    [McpServerTool]
    public async Task<Guild?> CreateGuild(string name, string? description, int ownerId)
    {
        var owner = await _db.Players.FindAsync(ownerId);
        if (owner == null) return null;

        var guild = new Guild
        {
            Name = name,
            Description = description,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Guilds.Add(guild);

        // Auto-add owner as member with "Leader" rank
        var member = new GuildMember
        {
            Guild = guild,
            PlayerId = ownerId,
            Rank = "Leader",
            JoinedAt = DateTime.UtcNow
        };
        _db.GuildMembers.Add(member);

        await _db.SaveChangesAsync();
        return guild;
    }

    /// <summary>
    /// Adds a member to a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="playerId">The player ID to add.</param>
    /// <param name="rank">The member's rank (e.g., Member, Officer, Leader).</param>
    [McpServerTool]
    public async Task<GuildMember?> AddGuildMember(int guildId, int playerId, string rank = "Member")
    {
        var guild = await _db.Guilds.FindAsync(guildId);
        var player = await _db.Players.FindAsync(playerId);
        if (guild == null || player == null) return null;

        // Check if already a member
        var existing = await _db.GuildMembers
            .FirstOrDefaultAsync(gm => gm.GuildId == guildId && gm.PlayerId == playerId);
        if (existing != null) return null;

        var member = new GuildMember
        {
            GuildId = guildId,
            PlayerId = playerId,
            Rank = rank,
            JoinedAt = DateTime.UtcNow
        };

        _db.GuildMembers.Add(member);
        await _db.SaveChangesAsync();
        return member;
    }

    /// <summary>
    /// Removes a member from a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="playerId">The player ID to remove.</param>
    [McpServerTool]
    public async Task<bool> RemoveGuildMember(int guildId, int playerId)
    {
        var member = await _db.GuildMembers
            .FirstOrDefaultAsync(gm => gm.GuildId == guildId && gm.PlayerId == playerId);
        if (member == null) return false;

        _db.GuildMembers.Remove(member);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Deletes a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    [McpServerTool]
    public async Task<bool> DeleteGuild(int id)
    {
        var guild = await _db.Guilds.FindAsync(id);
        if (guild == null) return false;

        // Remove all members first
        var members = await _db.GuildMembers
            .Where(gm => gm.GuildId == id)
            .ToListAsync();
        _db.GuildMembers.RemoveRange(members);

        _db.Guilds.Remove(guild);
        await _db.SaveChangesAsync();
        return true;
    }
}
