using Dapper;
using Hackathon.Entities;

namespace Hackathon.Services;

public class PlayerService
{
    private readonly DatabaseService _db;

    public PlayerService(DatabaseService db)
    {
        _db = db;
    }

    public Player? GetById(int id)
    {
        using var conn = _db.GetConnection();
        return conn.QuerySingleOrDefault<Player>("SELECT * FROM Player WHERE id = @id", new { id });
    }

    public Player? GetByDiscordId(string discordId)
    {
        using var conn = _db.GetConnection();
        return conn.QuerySingleOrDefault<Player>("SELECT * FROM Player WHERE discordId = @discordId", new { discordId });
    }

    public IEnumerable<Player> GetAll()
    {
        using var conn = _db.GetConnection();
        return conn.Query<Player>("SELECT * FROM Player");
    }
}

