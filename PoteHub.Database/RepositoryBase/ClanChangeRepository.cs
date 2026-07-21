using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class ClanChangeRepository : RepositoryBase
{
    public ClanChangeRepository(DatabaseConnection database)
        : base(database)
    {
    }

    public async Task SaveAsync(
        ClanChange change,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO ClanChanges
        (
            SyncRunId,
            SeasonId,
            ClanId,
            PreviousRank,
            CurrentRank,
            PreviousMemberCount,
            CurrentMemberCount,
            PreviousReputation,
            CurrentReputation,
            ReputationDifference,
            PreviousDeduction,
            CurrentDeduction,
            DetectedAt
        )
        VALUES
        (
            $syncRunId,
            $seasonId,
            $clanId,
            $previousRank,
            $currentRank,
            $previousMemberCount,
            $currentMemberCount,
            $previousReputation,
            $currentReputation,
            $difference,
            $previousDeduction,
            $currentDeduction,
            $detectedAt
        );
        """;

        command.Parameters.AddWithValue("$syncRunId", change.SyncRunId);
        command.Parameters.AddWithValue("$seasonId", change.SeasonId);
        command.Parameters.AddWithValue("$clanId", change.ClanId);
        command.Parameters.AddWithValue("$previousRank", change.PreviousRank);
        command.Parameters.AddWithValue("$currentRank", change.CurrentRank);
        command.Parameters.AddWithValue(
            "$previousReputation",
            change.PreviousReputation);

        command.Parameters.AddWithValue(
            "$currentReputation",
            change.CurrentReputation);

        command.Parameters.AddWithValue(
            "$difference",
            change.ReputationDifference);

        command.Parameters.AddWithValue(
            "$detectedAt",
            change.DetectedAt.ToString("O"));
        command.Parameters.AddWithValue(
    "$previousMemberCount",
    change.PreviousMemberCount);

        command.Parameters.AddWithValue(
            "$currentMemberCount",
            change.CurrentMemberCount);

        command.Parameters.AddWithValue(
            "$previousDeduction",
            change.PreviousDeduction);

        command.Parameters.AddWithValue(
            "$currentDeduction",
            change.CurrentDeduction);

        await command.ExecuteNonQueryAsync();
    }
}