using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class ClanSeasonRepository : RepositoryBase
{
    public ClanSeasonRepository(DatabaseConnection database)
        : base(database)
    {
    }

    public async Task SaveAsync(
    ClanSeason clanSeason,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO ClanSeasons
        (
            SeasonId,
            ClanId,
            Rank,
            MemberCount,
            Reputation,
            Deduction
        )
        VALUES
        (
            $seasonId,
            $clanId,
            $rank,
            $memberCount,
            $reputation,
            $deduction
        )
        ON CONFLICT(SeasonId, ClanId) DO UPDATE SET
            Rank = excluded.Rank,
            MemberCount = excluded.MemberCount,
            Reputation = excluded.Reputation,
            Deduction = excluded.Deduction;
        """;

        command.Parameters.AddWithValue("$seasonId", clanSeason.SeasonId);
        command.Parameters.AddWithValue("$clanId", clanSeason.ClanId);
        command.Parameters.AddWithValue("$rank", clanSeason.Rank);
        command.Parameters.AddWithValue("$memberCount", clanSeason.MemberCount);
        command.Parameters.AddWithValue("$reputation", clanSeason.Reputation);
        command.Parameters.AddWithValue("$deduction", clanSeason.Deduction);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<ClanSeason?> GetAsync(
    int seasonId,
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
        ClanId,
        Rank,
        MemberCount,
        Reputation,
        Deduction
    FROM ClanSeasons
    WHERE SeasonId = $seasonId
      AND ClanId = $clanId;
    """;

        command.Parameters.AddWithValue("$seasonId", seasonId);
        command.Parameters.AddWithValue("$clanId", clanId);

        using SqliteDataReader reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new ClanSeason
        {
            SeasonId = reader.GetInt32(0),
            ClanId = reader.GetInt32(1),
            Rank = reader.GetInt32(2),
            MemberCount = reader.GetInt32(3),
            Reputation = reader.GetInt32(4),
            Deduction = reader.GetInt32(5)
        };
    }
}