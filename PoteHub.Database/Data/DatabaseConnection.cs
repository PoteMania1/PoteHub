using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Data;

public class DatabaseConnection
{
    private readonly string _connectionString;

    public DatabaseConnection(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}