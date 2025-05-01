using Dapper;
using Discord;
using Discord.WebSocket;
using Hackathon.Entities;

namespace Hackathon.Services;

public class PlayerService(DatabaseService db, DiscordSocketClient discord)
{
    private readonly DatabaseService _db = db;

    private readonly DiscordSocketClient _client = discord;

    public Player? GetById(int id)
    {
        using var conn = _db.GetConnection();
        return conn.QuerySingleOrDefault<Player>("SELECT * FROM Player WHERE id = @id", new { id });
    }

    public Player? GetByDiscordId(string discordId)
    {
        using var conn = _db.GetConnection();
        return conn.QuerySingleOrDefault<Player>(
            "SELECT * FROM Player WHERE discordId = @discordId",
            new { discordId }
        );
    }

    public IEnumerable<Player> GetAll()
    {
        using var conn = _db.GetConnection();
        return conn.Query<Player>("SELECT * FROM Player");
    }
}
