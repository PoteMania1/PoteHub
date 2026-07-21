using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class MemberChangeRepository : RepositoryBase
{
    public MemberChangeRepository(DatabaseConnection database)
        : base(database)
    {
    }

    public async Task SaveAsync(
        MemberChange change,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO MemberChanges
        (
            SyncRunId,
            SeasonId,
            MemberId,
            ClanId,
            PreviousReputation,
            CurrentReputation,
            ReputationDifference,
            DetectedAt
        )
        VALUES
        (
            $syncRunId,
            $seasonId,
            $memberId,
            $clanId,
            $previousReputation,
            $currentReputation,
            $difference,
            $detectedAt
        );
        """;

        command.Parameters.AddWithValue("$syncRunId", change.SyncRunId);
        command.Parameters.AddWithValue("$seasonId", change.SeasonId);
        command.Parameters.AddWithValue("$memberId", change.MemberId);
        command.Parameters.AddWithValue("$clanId", change.ClanId);

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

        await command.ExecuteNonQueryAsync();
    }
}