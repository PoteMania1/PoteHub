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
            Reputation,
            IsActive,
            LastSeenAt
        )
        VALUES
        (
            $seasonId,
            $memberId,
            $clanId,
            $reputation,
            $isActive,
            $lastSeenAt
        )
        ON CONFLICT(SeasonId, MemberId, ClanId) DO UPDATE SET
            Reputation = excluded.Reputation,
            IsActive = excluded.IsActive,
            LastSeenAt = excluded.LastSeenAt;
        """;

        command.Parameters.AddWithValue(
         "$isActive",
         participation.IsActive ? 1 : 0);

        command.Parameters.AddWithValue(
            "$lastSeenAt",
            participation.LastSeenAt is null
                ? DBNull.Value
                : participation.LastSeenAt.Value.ToString("O"));

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

    public async Task<List<MemberParticipation>> GetActiveBySeasonAsync(
    int seasonId,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        List<MemberParticipation> participations = [];

        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
    SELECT
        SeasonId,
        MemberId,
        ClanId,
        Reputation,
        IsActive,
        LastSeenAt
    FROM MemberParticipations
    WHERE SeasonId = $seasonId
      AND IsActive = 1;
    """;

        command.Parameters.AddWithValue("$seasonId", seasonId);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            participations.Add(new MemberParticipation
            {
                SeasonId = reader.GetInt32(0),
                MemberId = reader.GetInt32(1),
                ClanId = reader.GetInt32(2),
                Reputation = reader.GetInt32(3),
                IsActive = reader.GetInt32(4) == 1,
                LastSeenAt = reader.IsDBNull(5)
                    ? null
                    : DateTime.Parse(reader.GetString(5))
            });
        }

        return participations;
    }

    public async Task DeactivateAsync(
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
    UPDATE MemberParticipations
    SET IsActive = 0
    WHERE SeasonId = $seasonId
      AND MemberId = $memberId
      AND ClanId = $clanId;
    """;

        command.Parameters.AddWithValue("$seasonId", seasonId);
        command.Parameters.AddWithValue("$memberId", memberId);
        command.Parameters.AddWithValue("$clanId", clanId);

        await command.ExecuteNonQueryAsync();
    }
    public async Task MarkRewardQualifiedAsync(
    int seasonId,
    int memberId,
    int clanId,
    DateTime qualifiedAt,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
    UPDATE MemberParticipations
    SET RewardQualifiedAt =
        COALESCE(
            RewardQualifiedAt,
            $qualifiedAt)
    WHERE SeasonId = $seasonId
      AND MemberId = $memberId
      AND ClanId = $clanId;
    """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        command.Parameters.AddWithValue(
            "$memberId",
            memberId);

        command.Parameters.AddWithValue(
            "$clanId",
            clanId);

        command.Parameters.AddWithValue(
            "$qualifiedAt",
            qualifiedAt.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }
}