using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using TibiaMcp.Server.Data;
using TibiaMcp.Server.Models;

namespace TibiaMcp.Server.Tools;

/// <summary>
/// MCP tools for managing Tibia players.
/// </summary>
[McpServerToolType]
public class PlayerTools
{
    private readonly AppDbContext _db;

    public PlayerTools(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Gets a list of all players with optional filters.
    /// </summary>
    /// <param name="vocation">Optional filter by vocation (e.g., Knight, Paladin, Sorcerer, Druid).</param>
    /// <param name="minLevel">Optional minimum level filter.</param>
    /// <param name="isOnline">Optional online status filter.</param>
    [McpServerTool]
    public async Task<List<Player>> GetPlayers(
        string? vocation = null,
        int? minLevel = null,
        bool? isOnline = null)
    {
        var query = _db.Players.AsQueryable();

        if (!string.IsNullOrWhiteSpace(vocation))
            query = query.Where(p => p.Vocation == vocation);

        if (minLevel.HasValue)
            query = query.Where(p => p.Level >= minLevel.Value);

        if (isOnline.HasValue)
            query = query.Where(p => p.IsOnline == isOnline.Value);

        return await query.OrderByDescending(p => p.Level).ToListAsync();
    }

    /// <summary>
    /// Gets a player by their unique identifier.
    /// </summary>
    /// <param name="id">The player ID.</param>
    [McpServerTool]
    public async Task<Player?> GetPlayerById(int id)
    {
        return await _db.Players.FindAsync(id);
    }

    /// <summary>
    /// Gets a player by their name.
    /// </summary>
    /// <param name="name">The player name.</param>
    [McpServerTool]
    public async Task<Player?> GetPlayerByName(string name)
    {
        return await _db.Players.FirstOrDefaultAsync(p => p.Name == name);
    }

    /// <summary>
    /// Creates a new player.
    /// </summary>
    /// <param name="name">The player's name.</param>
    /// <param name="vocation">The player's vocation (Knight, Paladin, Sorcerer, Druid).</param>
    /// <param name="level">The player's starting level (default: 1).</param>
    [McpServerTool]
    public async Task<Player> CreatePlayer(string name, string vocation, int level = 1)
    {
        var player = new Player
        {
            Name = name,
            Vocation = vocation,
            Level = level,
            Experience = 0,
            Health = 100,
            Mana = 50,
            IsOnline = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Players.Add(player);
        await _db.SaveChangesAsync();
        return player;
    }

    /// <summary>
    /// Updates a player's level and experience.
    /// </summary>
    /// <param name="id">The player ID.</param>
    /// <param name="level">The new level.</param>
    /// <param name="experience">The new experience amount.</param>
    [McpServerTool]
    public async Task<Player?> UpdatePlayerLevel(int id, int level, long? experience = null)
    {
        var player = await _db.Players.FindAsync(id);
        if (player == null) return null;

        player.Level = level;
        if (experience.HasValue)
            player.Experience = experience.Value;

        player.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return player;
    }

    /// <summary>
    /// Deletes a player by ID.
    /// </summary>
    /// <param name="id">The player ID.</param>
    [McpServerTool]
    public async Task<bool> DeletePlayer(int id)
    {
        var player = await _db.Players.FindAsync(id);
        if (player == null) return false;

        _db.Players.Remove(player);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Sets a player's online status.
    /// </summary>
    /// <param name="id">The player ID.</param>
    /// <param name="isOnline">Whether the player is online.</param>
    [McpServerTool]
    public async Task<Player?> SetPlayerOnlineStatus(int id, bool isOnline)
    {
        var player = await _db.Players.FindAsync(id);
        if (player == null) return null;

        player.IsOnline = isOnline;
        player.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return player;
    }
}
