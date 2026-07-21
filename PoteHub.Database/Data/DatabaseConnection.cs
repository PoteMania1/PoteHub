using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Data;

public class DatabaseConnection
{
    private readonly string _connectionString;

    public DatabaseConnection(string databasePath)
    {
        string? directory = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = true
        };

        _connectionString = builder.ToString();
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}