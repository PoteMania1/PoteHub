using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;

namespace PoteHub.Database.Migrations;

public class DatabaseMigrator
{
    private readonly DatabaseConnection _database;

    private readonly IReadOnlyList<IDatabaseMigration> _migrations;

    public DatabaseMigrator(DatabaseConnection database)
    {
        _database = database;

        _migrations =
        [
            new Migration001InitialSchema(),
            new Migration002SyncMetadata(),
            new Migration003Calendar(),
            new Migration004Statistics()
        ];
    }

    public async Task MigrateAsync()
    {
        using SqliteConnection connection =
            _database.CreateConnection();

        await connection.OpenAsync();

        using SqliteTransaction transaction =
            connection.BeginTransaction();

        try
        {
            await CreateMigrationTableAsync(
                connection,
                transaction);

            HashSet<int> appliedVersions =
                await GetAppliedVersionsAsync(
                    connection,
                    transaction);

            foreach (IDatabaseMigration migration
                     in _migrations.OrderBy(m => m.Version))
            {
                if (appliedVersions.Contains(migration.Version))
                {
                    continue;
                }

                await migration.ApplyAsync(
                    connection,
                    transaction);

                await RegisterMigrationAsync(
                    migration,
                    connection,
                    transaction);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task CreateMigrationTableAsync(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS SchemaMigrations
        (
            Version INTEGER PRIMARY KEY,
            Name TEXT NOT NULL,
            AppliedAt TEXT NOT NULL
        );
        """;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<HashSet<int>>
        GetAppliedVersionsAsync(
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        HashSet<int> versions = [];

        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        SELECT Version
        FROM SchemaMigrations;
        """;

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            versions.Add(reader.GetInt32(0));
        }

        return versions;
    }

    private static async Task RegisterMigrationAsync(
        IDatabaseMigration migration,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO SchemaMigrations
        (
            Version,
            Name,
            AppliedAt
        )
        VALUES
        (
            $version,
            $name,
            $appliedAt
        );
        """;

        command.Parameters.AddWithValue(
            "$version",
            migration.Version);

        command.Parameters.AddWithValue(
            "$name",
            migration.Name);

        command.Parameters.AddWithValue(
            "$appliedAt",
            DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }
}