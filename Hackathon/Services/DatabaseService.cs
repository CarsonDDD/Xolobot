using System.Data.SQLite;
using Microsoft.Extensions.Logging;

namespace Hackathon.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public DatabaseService(string dbPath, ILogger<DatabaseService> logger)
    {
        _connectionString = $"Data Source={dbPath};Version=3;";

        _logger = logger;
        _logger.LogInformation($"Attempting to connect to db: {dbPath}");
        //_logger.LogInformation("Absolute DB path: " + Path.GetFullPath("my_database.db"));


        try
        {
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            _logger.LogInformation("Database connection established successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to database.");
            throw;
        }
    }

    public SQLiteConnection GetConnection()
    {
        var conn = new SQLiteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
