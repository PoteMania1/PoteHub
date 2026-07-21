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

    public async Task<long> CreateAsync(
        SyncRun syncRun,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO SyncRuns
        (
            SeasonId,
            ExecutedAt,
            ClanChanges,
            MemberChanges
        )
        VALUES
        (
            $seasonId,
            $executedAt,
            0,
            0
        );

        SELECT last_insert_rowid();
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            syncRun.SeasonId);

        command.Parameters.AddWithValue(
            "$executedAt",
            syncRun.ExecutedAt.ToString("O"));

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt64(result);
    }

    public async Task UpdateTotalsAsync(
        long syncRunId,
        int clanChanges,
        int memberChanges,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        UPDATE SyncRuns
        SET ClanChanges = $clanChanges,
            MemberChanges = $memberChanges
        WHERE SyncRunId = $syncRunId;
        """;

        command.Parameters.AddWithValue("$syncRunId", syncRunId);
        command.Parameters.AddWithValue("$clanChanges", clanChanges);
        command.Parameters.AddWithValue("$memberChanges", memberChanges);

        await command.ExecuteNonQueryAsync();
    }
}