using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class SyncRunRepository : RepositoryBase
{
    public SyncRunRepository(DatabaseConnection database)
        : base(database)
    {
    }

    public async Task<bool> HasAnyBySeasonAsync(
        int seasonId,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        SELECT EXISTS
        (
            SELECT 1
            FROM SyncRuns
            WHERE SeasonId = $seasonId
        );
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) == 1;
    }

    public async Task<bool> ExistsByGeneratedAtAsync(
        int seasonId,
        DateTime generatedAt,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        SELECT EXISTS
        (
            SELECT 1
            FROM SyncRuns
            WHERE SeasonId = $seasonId
              AND GeneratedAt = $generatedAt
        );
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        command.Parameters.AddWithValue(
            "$generatedAt",
            generatedAt.ToString("O"));

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) == 1;
    }

    public async Task<long> CreateAsync(
        SyncRun syncRun,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO SyncRuns
        (
            SeasonId,
            DayId,
            WaveId,
            ExecutedAt,
            GeneratedAt,
            ClanChanges,
            MemberChanges,
            EnteredMembers,
            ChangedClanMembers,
            MissingMembers
        )
        VALUES
        (
            $seasonId,
            $dayId,
            $waveId,
            $executedAt,
            $generatedAt,
            0,
            0,
            0,
            0,
            0
        );

        SELECT last_insert_rowid();
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            syncRun.SeasonId);

        command.Parameters.AddWithValue(
            "$dayId",
            syncRun.DayId);

        command.Parameters.AddWithValue(
            "$waveId",
            syncRun.WaveId);

        command.Parameters.AddWithValue(
            "$executedAt",
            syncRun.ExecutedAt.ToString("O"));

        command.Parameters.AddWithValue(
            "$generatedAt",
            syncRun.GeneratedAt.ToString("O"));

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt64(result);
    }

    public async Task UpdateTotalsAsync(
        long syncRunId,
        int clanChanges,
        int memberChanges,
        int enteredMembers,
        int changedClanMembers,
        int missingMembers,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        UPDATE SyncRuns
        SET ClanChanges = $clanChanges,
            MemberChanges = $memberChanges,
            EnteredMembers = $enteredMembers,
            ChangedClanMembers = $changedClanMembers,
            MissingMembers = $missingMembers
        WHERE SyncRunId = $syncRunId;
        """;

        command.Parameters.AddWithValue(
            "$syncRunId",
            syncRunId);

        command.Parameters.AddWithValue(
            "$clanChanges",
            clanChanges);

        command.Parameters.AddWithValue(
            "$memberChanges",
            memberChanges);

        command.Parameters.AddWithValue(
            "$enteredMembers",
            enteredMembers);

        command.Parameters.AddWithValue(
            "$changedClanMembers",
            changedClanMembers);

        command.Parameters.AddWithValue(
            "$missingMembers",
            missingMembers);

        await command.ExecuteNonQueryAsync();
    }
}