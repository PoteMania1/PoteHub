using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;
using PoteHub.Domain.Enums;

namespace PoteHub.Database.RepositoryBase;

public class DiscordUserRepository : RepositoryBase
{
    public DiscordUserRepository(
        DatabaseConnection database)
        : base(database)
    {
    }

    public async Task<DiscordLinkResult>
        LinkAsync(
            string discordId,
            int memberId)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteTransaction transaction =
            connection.BeginTransaction();

        try
        {
            DiscordLinkResult? member =
                await FindMemberAsync(
                    memberId,
                    connection,
                    transaction);

            if (member is null)
            {
                await transaction.RollbackAsync();

                return new DiscordLinkResult
                {
                    Status =
                        DiscordLinkStatus
                            .MemberNotFound,

                    MemberId = memberId
                };
            }

            bool linkedToAnotherUser =
                await IsLinkedToAnotherUserAsync(
                    discordId,
                    memberId,
                    connection,
                    transaction);

            if (linkedToAnotherUser)
            {
                await transaction.RollbackAsync();

                member.Status =
                    DiscordLinkStatus
                        .MemberAlreadyLinked;

                return member;
            }

            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
            """
            INSERT INTO DiscordUsers
            (
                DiscordId,
                MemberId,
                LinkedAt,
                UpdatedAt,
                IsActive
            )
            VALUES
            (
                $discordId,
                $memberId,
                $linkedAt,
                $updatedAt,
                1
            )
            ON CONFLICT(DiscordId) DO UPDATE SET
                MemberId = excluded.MemberId,
                UpdatedAt = excluded.UpdatedAt,
                IsActive = 1;
            """;

            string timestamp =
                DateTime.UtcNow.ToString("O");

            command.Parameters.AddWithValue(
                "$discordId",
                discordId);

            command.Parameters.AddWithValue(
                "$memberId",
                memberId);

            command.Parameters.AddWithValue(
                "$linkedAt",
                timestamp);

            command.Parameters.AddWithValue(
                "$updatedAt",
                timestamp);

            await command.ExecuteNonQueryAsync();

            await transaction.CommitAsync();

            member.Status =
                DiscordLinkStatus.Success;

            return member;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<DiscordLinkResult?>
        FindMemberAsync(
            int memberId,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        SELECT
            m.MemberId,
            m.Name,
            COALESCE(c.Name, 'Sin clan')

        FROM Members m

        LEFT JOIN MemberParticipations p
            ON p.MemberId = m.MemberId
           AND p.IsActive = 1

        LEFT JOIN Clans c
            ON c.ClanId = p.ClanId

        WHERE m.MemberId = $memberId

        LIMIT 1;
        """;

        command.Parameters.AddWithValue(
            "$memberId",
            memberId);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new DiscordLinkResult
        {
            MemberId = reader.GetInt32(0),
            MemberName = reader.GetString(1),
            ClanName = reader.GetString(2)
        };
    }

    private static async Task<bool>
        IsLinkedToAnotherUserAsync(
            string discordId,
            int memberId,
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
            FROM DiscordUsers
            WHERE MemberId = $memberId
              AND DiscordId <> $discordId
              AND IsActive = 1
        );
        """;

        command.Parameters.AddWithValue(
            "$memberId",
            memberId);

        command.Parameters.AddWithValue(
            "$discordId",
            discordId);

        object? result =
            await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) == 1;
    }

    public async Task<MemberProfile?> GetProfileAsync(
    string discordId)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
    WITH CurrentSeason AS
    (
        SELECT
            SeasonId,
            Name

        FROM Seasons

        ORDER BY
            EndTime DESC

        LIMIT 1
    ),

    ActiveRanking AS
    (
        SELECT
            p.SeasonId,
            p.MemberId,
            p.ClanId,
            p.Reputation,
            p.RewardQualifiedAt,

            RANK() OVER
            (
                ORDER BY
                    p.Reputation DESC
            ) AS GlobalRank

        FROM MemberParticipations p

        JOIN CurrentSeason cs
            ON cs.SeasonId = p.SeasonId

        WHERE p.IsActive = 1
    ),

    CurrentWave AS
    (
        SELECT
            sr.WaveId,
            sr.GeneratedAt,
            d.DayNumber,
            w.WaveNumber,
            w.StartTime,
            w.EndTime,
            w.Status

        FROM SyncRuns sr

        JOIN Waves w
            ON w.WaveId = sr.WaveId

        JOIN Days d
            ON d.DayId = sr.DayId

        WHERE sr.WaveId IS NOT NULL
          AND sr.DayId IS NOT NULL

        ORDER BY
            sr.GeneratedAt DESC

        LIMIT 1
    ),

    MemberWaveTotals AS
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

        JOIN CurrentWave cw
            ON cw.WaveId = sr.WaveId

        GROUP BY
            mc.MemberId,
            mc.ClanId
    )

    SELECT
        m.MemberId,
        m.Name,
        m.Level,
        c.ClanId,
        c.Name,
        cs.SeasonId,
        cs.Name,
        ar.Reputation,
        ar.GlobalRank,
        ar.RewardQualifiedAt,

        cw.DayNumber,
        cw.WaveNumber,
        cw.StartTime,
        cw.EndTime,
        cw.Status,

        COALESCE(
            mwt.ReputationGain,
            0
        ),

        COALESCE(
            mwt.ReputationDeduction,
            0
        ),

        COALESCE(
            mwt.NetReputation,
            0
        ),

        cw.GeneratedAt

    FROM DiscordUsers du

    JOIN Members m
        ON m.MemberId = du.MemberId

    JOIN ActiveRanking ar
        ON ar.MemberId = m.MemberId

    JOIN Clans c
        ON c.ClanId = ar.ClanId

    JOIN CurrentSeason cs
        ON cs.SeasonId = ar.SeasonId

    CROSS JOIN CurrentWave cw

    LEFT JOIN MemberWaveTotals mwt
        ON mwt.MemberId = ar.MemberId
       AND mwt.ClanId = ar.ClanId

    WHERE du.DiscordId = $discordId
      AND du.IsActive = 1

    LIMIT 1;
    """;

        command.Parameters.AddWithValue(
            "$discordId",
            discordId);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        const int requiredReputation = 10000;

        int currentReputation =
            reader.GetInt32(7);

        int remainingReputation = Math.Max(
            0,
            requiredReputation -
            currentReputation);

        decimal progressPercentage = Math.Min(
            100m,
            currentReputation * 100m /
            requiredReputation);

        DateTime? qualifiedAt =
            reader.IsDBNull(9)
                ? null
                : DateTime.Parse(
                    reader.GetString(9));

        return new MemberProfile
        {
            MemberId = reader.GetInt32(0),
            MemberName = reader.GetString(1),
            Level = reader.GetInt32(2),
            ClanId = reader.GetInt32(3),
            ClanName = reader.GetString(4),
            SeasonId = reader.GetInt32(5),
            SeasonName = reader.GetString(6),
            CurrentReputation = currentReputation,

            GlobalRank =
                Convert.ToInt32(
                    reader.GetInt64(8)),

            RequiredReputation =
                requiredReputation,

            RemainingReputation =
                remainingReputation,

            ProgressPercentage =
                Math.Round(
                    progressPercentage,
                    2),

            IsRewardEligible =
                qualifiedAt is not null,

            RewardQualifiedAt =
                qualifiedAt,

            DayNumber =
                reader.GetInt32(10),

            WaveNumber =
                reader.GetInt32(11),

            WaveStartTime =
                DateTime.Parse(
                    reader.GetString(12)),

            WaveEndTime =
                DateTime.Parse(
                    reader.GetString(13)),

            WaveStatus =
                reader.GetString(14),

            WaveReputationGain =
                reader.GetInt32(15),

            WaveReputationDeduction =
                reader.GetInt32(16),

            WaveNetReputation =
                reader.GetInt32(17),

            LastUpdatedAt =
                DateTime.Parse(
                    reader.GetString(18))
        };
    }

}