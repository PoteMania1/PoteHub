using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class MemberMovementRepository : RepositoryBase
{
    public MemberMovementRepository(DatabaseConnection database)
        : base(database)
    {
    }

    public async Task SaveAsync(
        MemberMovement movement,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO MemberMovements
        (
            SyncRunId,
            SeasonId,
            MemberId,
            FromClanId,
            ToClanId,
            MovementType,
            DetectedAt
        )
        VALUES
        (
            $syncRunId,
            $seasonId,
            $memberId,
            $fromClanId,
            $toClanId,
            $movementType,
            $detectedAt
        );
        """;

        command.Parameters.AddWithValue(
            "$syncRunId",
            movement.SyncRunId);

        command.Parameters.AddWithValue(
            "$seasonId",
            movement.SeasonId);

        command.Parameters.AddWithValue(
            "$memberId",
            movement.MemberId);

        command.Parameters.AddWithValue(
            "$fromClanId",
            movement.FromClanId is null
                ? DBNull.Value
                : movement.FromClanId.Value);

        command.Parameters.AddWithValue(
            "$toClanId",
            movement.ToClanId is null
                ? DBNull.Value
                : movement.ToClanId.Value);

        command.Parameters.AddWithValue(
            "$movementType",
            movement.MovementType);

        command.Parameters.AddWithValue(
            "$detectedAt",
            movement.DetectedAt.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }
}