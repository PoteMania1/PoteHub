using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Migrations;

public class Migration003Calendar : IDatabaseMigration
{
    public int Version => 3;

    public string Name => "Add days and waves";

    public async Task ApplyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS Days
        (
            DayId INTEGER PRIMARY KEY AUTOINCREMENT,
            SeasonId INTEGER NOT NULL,
            DayNumber INTEGER NOT NULL,
            ServerDate TEXT NOT NULL,
            StartTime TEXT NOT NULL,
            EndTime TEXT NOT NULL,
            IsCompleted INTEGER NOT NULL DEFAULT 0,

            FOREIGN KEY (SeasonId)
                REFERENCES Seasons(SeasonId),

            UNIQUE (SeasonId, DayNumber),
            UNIQUE (SeasonId, ServerDate)
        );

        CREATE TABLE IF NOT EXISTS Waves
        (
            WaveId INTEGER PRIMARY KEY AUTOINCREMENT,
            DayId INTEGER NOT NULL,
            SeasonId INTEGER NOT NULL,
            WaveNumber INTEGER NOT NULL,
            StartTime TEXT NOT NULL,
            EndTime TEXT NOT NULL,
            Status TEXT NOT NULL DEFAULT 'Pending',
            SuccessfulSyncCount INTEGER NOT NULL DEFAULT 0,
            FirstGeneratedAt TEXT NULL,
            LastGeneratedAt TEXT NULL,
            CompletedAt TEXT NULL,

            FOREIGN KEY (DayId)
                REFERENCES Days(DayId),

            FOREIGN KEY (SeasonId)
                REFERENCES Seasons(SeasonId),

            UNIQUE (DayId, WaveNumber)
        );

        CREATE INDEX IF NOT EXISTS IX_Days_SeasonId
            ON Days(SeasonId);

        CREATE INDEX IF NOT EXISTS IX_Waves_SeasonId
            ON Waves(SeasonId);

        CREATE INDEX IF NOT EXISTS IX_Waves_DayId
            ON Waves(DayId);

        CREATE INDEX IF NOT EXISTS IX_Waves_Status
            ON Waves(Status);
        """;

        await command.ExecuteNonQueryAsync();

        await AddColumnIfMissingAsync(
            connection,
            transaction,
            "SyncRuns",
            "DayId",
            "INTEGER NULL");

        await AddColumnIfMissingAsync(
            connection,
            transaction,
            "SyncRuns",
            "WaveId",
            "INTEGER NULL");

        using SqliteCommand indexCommand =
            connection.CreateCommand();

        indexCommand.Transaction = transaction;

        indexCommand.CommandText =
        """
        CREATE INDEX IF NOT EXISTS IX_SyncRuns_DayId
            ON SyncRuns(DayId);

        CREATE INDEX IF NOT EXISTS IX_SyncRuns_WaveId
            ON SyncRuns(WaveId);
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