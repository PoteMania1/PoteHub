using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class MemberParticipationRepository : RepositoryBase
{
    public MemberParticipationRepository(DatabaseConnection database)
        : base(database)
    {
    }

    public async Task SaveAsync(
    MemberParticipation participation,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO MemberParticipations
        (
            SeasonId,
            MemberId,
            ClanId,
            Reputation
        )
        VALUES
        (
            $seasonId,
            $memberId,
            $clanId,
            $reputation
        )
        ON CONFLICT(SeasonId, MemberId, ClanId) DO UPDATE SET
            Reputation = excluded.Reputation;
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            participation.SeasonId);

        command.Parameters.AddWithValue(
            "$memberId",
            participation.MemberId);

        command.Parameters.AddWithValue(
            "$clanId",
            participation.ClanId);

        command.Parameters.AddWithValue(
            "$reputation",
            participation.Reputation);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<MemberParticipation?> GetAsync(
    int seasonId,
    int memberId,
    int clanId,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
    SELECT
        SeasonId,
        MemberId,
        ClanId,
        Reputation
    FROM MemberParticipations
    WHERE SeasonId = $seasonId
      AND MemberId = $memberId
      AND ClanId = $clanId;
    """;

        command.Parameters.AddWithValue("$seasonId", seasonId);
        command.Parameters.AddWithValue("$memberId", memberId);
        command.Parameters.AddWithValue("$clanId", clanId);

        using SqliteDataReader reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new MemberParticipation
        {
            SeasonId = reader.GetInt32(0),
            MemberId = reader.GetInt32(1),
            ClanId = reader.GetInt32(2),
            Reputation = reader.GetInt32(3)
        };
    }
}