using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Migrations;

public class Migration002SyncMetadata : IDatabaseMigration
{
    public int Version => 2;

    public string Name => "Add synchronization metadata";

    public async Task ApplyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        await AddColumnIfMissingAsync(
            connection,
            transaction,
            "SyncRuns",
            "GeneratedAt",
            "TEXT NULL");

        await AddColumnIfMissingAsync(
            connection,
            transaction,
            "SyncRuns",
            "EnteredMembers",
            "INTEGER NOT NULL DEFAULT 0");

        await AddColumnIfMissingAsync(
            connection,
            transaction,
            "SyncRuns",
            "ChangedClanMembers",
            "INTEGER NOT NULL DEFAULT 0");

        await AddColumnIfMissingAsync(
            connection,
            transaction,
            "SyncRuns",
            "MissingMembers",
            "INTEGER NOT NULL DEFAULT 0");

        using SqliteCommand indexCommand =
            connection.CreateCommand();

        indexCommand.Transaction = transaction;

        indexCommand.CommandText =
        """
        CREATE UNIQUE INDEX IF NOT EXISTS
            UX_SyncRuns_Season_GeneratedAt
        ON SyncRuns(SeasonId, GeneratedAt)
        WHERE GeneratedAt IS NOT NULL;
        """;

        await indexCommand.ExecuteNonQueryAsync();
    }

    private static async Task AddColumnIfMissingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        using SqliteCommand checkCommand =
            connection.CreateCommand();

        checkCommand.Transaction = transaction;
        checkCommand.CommandText =
            $"PRAGMA table_info({tableName});";

        bool exists = false;

        using (SqliteDataReader reader =
               await checkCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                string existingName = reader.GetString(1);

                if (existingName.Equals(
                    columnName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
        {
            return;
        }

        using SqliteCommand alterCommand =
            connection.CreateCommand();

        alterCommand.Transaction = transaction;

        alterCommand.CommandText =
            $"ALTER TABLE {tableName} " +
            $"ADD COLUMN {columnName} {columnDefinition};";

        await alterCommand.ExecuteNonQueryAsync();
    }
}