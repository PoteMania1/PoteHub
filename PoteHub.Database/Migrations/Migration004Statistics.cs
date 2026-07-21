using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Migrations;

public class Migration004Statistics : IDatabaseMigration
{
    public int Version => 4;

    public string Name =>
        "Add statistics and reward qualification";

    public async Task ApplyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        await AddColumnIfMissingAsync(
            connection,
            transaction,
            "MemberParticipations",
            "RewardQualifiedAt",
            "TEXT NULL");

        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        CREATE INDEX IF NOT EXISTS
            IX_MemberParticipations_ActiveReputation
        ON MemberParticipations
        (
            SeasonId,
            IsActive,
            Reputation
        );

        CREATE INDEX IF NOT EXISTS
            IX_MemberChanges_SeasonClanMember
        ON MemberChanges
        (
            SeasonId,
            ClanId,
            MemberId
        );

        CREATE INDEX IF NOT EXISTS
            IX_SyncRuns_WaveGeneratedAt
        ON SyncRuns
        (
            WaveId,
            GeneratedAt
        );
        """;

        await command.ExecuteNonQueryAsync();
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
                string existingName =
                    reader.GetString(1);

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
            $"ADD COLUMN {columnName} " +
            $"{columnDefinition};";

        await alterCommand.ExecuteNonQueryAsync();
    }
}