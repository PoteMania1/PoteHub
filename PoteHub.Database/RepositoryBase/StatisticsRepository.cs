using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class StatisticsRepository : RepositoryBase
{
    public StatisticsRepository(DatabaseConnection database)
        : base(database)
    {
    }

    public async Task<List<MemberRankingEntry>>
    GetGlobalRankingAsync(
        int seasonId,
        int limit,
        SqliteConnection connection)
    {
        List<MemberRankingEntry> ranking = [];

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
    SELECT
        RANK() OVER
        (
            ORDER BY
                p.Reputation DESC
        ) AS GlobalRank,

        m.MemberId,
        m.Name,
        c.ClanId,
        c.Name,
        p.Reputation

    FROM MemberParticipations p

    JOIN Members m
        ON m.MemberId = p.MemberId

    JOIN Clans c
        ON c.ClanId = p.ClanId

    WHERE p.SeasonId = $seasonId
      AND p.IsActive = 1

    ORDER BY
        p.Reputation DESC,
        m.MemberId ASC

    LIMIT $limit;
    """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        command.Parameters.AddWithValue(
            "$limit",
            limit);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            ranking.Add(new MemberRankingEntry
            {
                Rank = reader.GetInt32(0),
                MemberId = reader.GetInt32(1),
                MemberName = reader.GetString(2),
                ClanId = reader.GetInt32(3),
                ClanName = reader.GetString(4),
                CurrentReputation = reader.GetInt32(5)
            });
        }

        return ranking;
    }

    public async Task<List<MemberRankingEntry>>
    GetDailyRankingAsync(
        int seasonId,
        int dayNumber,
        int limit,
        SqliteConnection connection)
    {
        List<MemberRankingEntry> ranking = [];

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
    WITH DailyTotals AS
    (
        SELECT
            mc.MemberId,
            mc.ClanId,

            SUM
            (
                CASE
                    WHEN mc.ReputationDifference > 0
                    THEN mc.ReputationDifference
                    ELSE 0
                END
            ) AS ReputationGain,

            SUM
            (
                CASE
                    WHEN mc.ReputationDifference < 0
                    THEN ABS(mc.ReputationDifference)
                    ELSE 0
                END
            ) AS ReputationDeduction,

            SUM(
                mc.ReputationDifference
            ) AS NetReputation

        FROM MemberChanges mc

        JOIN SyncRuns sr
            ON sr.SyncRunId = mc.SyncRunId

        JOIN Days d
            ON d.DayId = sr.DayId

        WHERE mc.SeasonId = $seasonId
          AND d.DayNumber = $dayNumber

        GROUP BY
            mc.MemberId,
            mc.ClanId
    ),

    Ranked AS
    (
        SELECT
            RANK() OVER
            (
                ORDER BY
                    dt.ReputationGain DESC
            ) AS DailyRank,

            dt.MemberId,
            dt.ClanId,
            dt.ReputationGain,
            dt.ReputationDeduction,
            dt.NetReputation

        FROM DailyTotals dt
    )

    SELECT
        r.DailyRank,
        m.MemberId,
        m.Name,
        c.ClanId,
        c.Name,
        p.Reputation,
        r.ReputationGain,
        r.ReputationDeduction,
        r.NetReputation

    FROM Ranked r

    JOIN Members m
        ON m.MemberId = r.MemberId

    JOIN Clans c
        ON c.ClanId = r.ClanId

    LEFT JOIN MemberParticipations p
        ON p.SeasonId = $seasonId
       AND p.MemberId = r.MemberId
       AND p.ClanId = r.ClanId

    ORDER BY
        r.DailyRank,
        m.MemberId

    LIMIT $limit;
    """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        command.Parameters.AddWithValue(
            "$dayNumber",
            dayNumber);

        command.Parameters.AddWithValue(
            "$limit",
            limit);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            ranking.Add(ReadRankingEntry(reader));
        }

        return ranking;
    }

    public async Task<List<MemberRankingEntry>>
    GetWaveRankingAsync(
        long waveId,
        int limit,
        SqliteConnection connection)
    {
        List<MemberRankingEntry> ranking = [];

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
    WITH WaveTotals AS
    (
        SELECT
            mc.SeasonId,
            mc.MemberId,
            mc.ClanId,

            SUM
            (
                CASE
                    WHEN mc.ReputationDifference > 0
                    THEN mc.ReputationDifference
                    ELSE 0
                END
            ) AS ReputationGain,

            SUM
            (
                CASE
                    WHEN mc.ReputationDifference < 0
                    THEN ABS(mc.ReputationDifference)
                    ELSE 0
                END
            ) AS ReputationDeduction,

            SUM(
                mc.ReputationDifference
            ) AS NetReputation

        FROM MemberChanges mc

        JOIN SyncRuns sr
            ON sr.SyncRunId = mc.SyncRunId

        WHERE sr.WaveId = $waveId

        GROUP BY
            mc.SeasonId,
            mc.MemberId,
            mc.ClanId
    ),

    Ranked AS
    (
        SELECT
            RANK() OVER
            (
                ORDER BY
                    wt.ReputationGain DESC
            ) AS WaveRank,

            wt.SeasonId,
            wt.MemberId,
            wt.ClanId,
            wt.ReputationGain,
            wt.ReputationDeduction,
            wt.NetReputation

        FROM WaveTotals wt
    )

    SELECT
        r.WaveRank,
        m.MemberId,
        m.Name,
        c.ClanId,
        c.Name,
        p.Reputation,
        r.ReputationGain,
        r.ReputationDeduction,
        r.NetReputation

    FROM Ranked r

    JOIN Members m
        ON m.MemberId = r.MemberId

    JOIN Clans c
        ON c.ClanId = r.ClanId

    LEFT JOIN MemberParticipations p
        ON p.SeasonId = r.SeasonId
       AND p.MemberId = r.MemberId
       AND p.ClanId = r.ClanId

    ORDER BY
        r.WaveRank,
        m.MemberId

    LIMIT $limit;
    """;

        command.Parameters.AddWithValue(
            "$waveId",
            waveId);

        command.Parameters.AddWithValue(
            "$limit",
            limit);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            ranking.Add(ReadRankingEntry(reader));
        }

        return ranking;
    }

    private static MemberRankingEntry ReadRankingEntry(
    SqliteDataReader reader)
    {
        return new MemberRankingEntry
        {
            Rank = reader.GetInt32(0),
            MemberId = reader.GetInt32(1),
            MemberName = reader.GetString(2),
            ClanId = reader.GetInt32(3),
            ClanName = reader.GetString(4),

            CurrentReputation = reader.IsDBNull(5)
                ? 0
                : reader.GetInt32(5),

            ReputationGain = reader.GetInt32(6),
            ReputationDeduction = reader.GetInt32(7),
            NetReputation = reader.GetInt32(8)
        };
    }
}