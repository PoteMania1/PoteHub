using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class DiscordWaveReportRepository
    : RepositoryBase
{
    public DiscordWaveReportRepository(
        DatabaseConnection database)
        : base(database)
    {
    }

    public async Task ConfigureAsync(
        string guildId,
        string channelId,
        int clanId)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        long? currentWaveId =
            await GetCurrentWaveIdAsync(
                connection);

        if (currentWaveId is null)
        {
            throw new InvalidOperationException(
                "Todavía no existe una wave sincronizada.");
        }

        string timestamp =
            DateTime.UtcNow.ToString("O");

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        INSERT INTO DiscordWaveReportSettings
        (
            GuildId,
            ChannelId,
            ClanId,
            StartWaveId,
            IsActive,
            CreatedAt,
            UpdatedAt
        )
        VALUES
        (
            $guildId,
            $channelId,
            $clanId,
            $startWaveId,
            1,
            $createdAt,
            $updatedAt
        )
        ON CONFLICT(GuildId)
        DO UPDATE SET
            ChannelId = excluded.ChannelId,
            ClanId = excluded.ClanId,
            StartWaveId = excluded.StartWaveId,
            IsActive = 1,
            UpdatedAt = excluded.UpdatedAt;
        """;

        command.Parameters.AddWithValue(
            "$guildId",
            guildId);

        command.Parameters.AddWithValue(
            "$channelId",
            channelId);

        command.Parameters.AddWithValue(
            "$clanId",
            clanId);

        command.Parameters.AddWithValue(
            "$startWaveId",
            currentWaveId.Value);

        command.Parameters.AddWithValue(
            "$createdAt",
            timestamp);

        command.Parameters.AddWithValue(
            "$updatedAt",
            timestamp);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<DiscordWaveReportSetting>>
        GetActiveSettingsAsync()
    {
        List<DiscordWaveReportSetting> settings = [];

        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            wrs.GuildId,
            wrs.ChannelId,
            wrs.ClanId,
            c.Name,
            wrs.StartWaveId,
            wrs.IsActive

        FROM DiscordWaveReportSettings wrs

        JOIN Clans c
            ON c.ClanId = wrs.ClanId

        WHERE wrs.IsActive = 1;
        """;

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            settings.Add(
                new DiscordWaveReportSetting
                {
                    GuildId = reader.GetString(0),
                    ChannelId = reader.GetString(1),
                    ClanId = reader.GetInt32(2),
                    ClanName = reader.GetString(3),
                    StartWaveId = reader.GetInt64(4),
                    IsActive =
                        reader.GetInt32(5) == 1
                });
        }

        return settings;
    }

    public async Task<List<long>>
        GetPendingWaveIdsAsync(
            DiscordWaveReportSetting setting)
    {
        List<long> waveIds = [];

        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT w.WaveId

        FROM Waves w

        WHERE w.WaveId >= $startWaveId

          AND w.Status IN
          (
              'Complete',
              'Incomplete'
          )

          AND w.SuccessfulSyncCount > 0

          AND NOT EXISTS
          (
              SELECT 1

              FROM DiscordWaveReports dwr

              WHERE dwr.GuildId = $guildId
                AND dwr.WaveId = w.WaveId
          )

        ORDER BY w.WaveId;
        """;

        command.Parameters.AddWithValue(
            "$startWaveId",
            setting.StartWaveId);

        command.Parameters.AddWithValue(
            "$guildId",
            setting.GuildId);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            waveIds.Add(reader.GetInt64(0));
        }

        return waveIds;
    }

    public async Task<DiscordWaveReportData?>
        GetReportAsync(
            long waveId,
            int clanId,
            int memberLimit = 10)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        DiscordWaveReportData? report =
            await GetContextAsync(
                waveId,
                clanId,
                connection);

        if (report is null)
        {
            return null;
        }

        await LoadClanTotalsAsync(
            report,
            connection);

        await LoadMembersAsync(
            report,
            memberLimit,
            connection);

        return report;
    }

    private static async Task<DiscordWaveReportData?>
        GetContextAsync(
            long waveId,
            int clanId,
            SqliteConnection connection)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            s.SeasonId,
            s.Name,
            d.DayId,
            d.DayNumber,
            w.WaveId,
            w.WaveNumber,
            w.StartTime,
            w.EndTime,
            w.Status,
            c.ClanId,
            c.Name

        FROM Waves w

        JOIN Days d
            ON d.DayId = w.DayId

        JOIN Seasons s
            ON s.SeasonId = w.SeasonId

        JOIN Clans c
            ON c.ClanId = $clanId

        WHERE w.WaveId = $waveId

        LIMIT 1;
        """;

        command.Parameters.AddWithValue(
            "$waveId",
            waveId);

        command.Parameters.AddWithValue(
            "$clanId",
            clanId);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new DiscordWaveReportData
        {
            SeasonId = reader.GetInt32(0),
            SeasonName = reader.GetString(1),
            DayId = reader.GetInt64(2),
            DayNumber = reader.GetInt32(3),
            WaveId = reader.GetInt64(4),
            WaveNumber = reader.GetInt32(5),
            StartTime =
                DateTime.Parse(reader.GetString(6)),
            EndTime =
                DateTime.Parse(reader.GetString(7)),
            Status = reader.GetString(8),
            ClanId = reader.GetInt32(9),
            ClanName = reader.GetString(10)
        };
    }

    private static async Task LoadClanTotalsAsync(
        DiscordWaveReportData report,
        SqliteConnection connection)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            COALESCE
            (
                SUM
                (
                    CASE
                        WHEN cc.ReputationDifference > 0
                        THEN cc.ReputationDifference
                        ELSE 0
                    END
                ),
                0
            ),

            COALESCE
            (
                SUM
                (
                    CASE
                        WHEN cc.ReputationDifference < 0
                        THEN ABS(cc.ReputationDifference)
                        ELSE 0
                    END
                ),
                0
            ),

            COALESCE
            (
                SUM(cc.ReputationDifference),
                0
            )

        FROM ClanChanges cc

        JOIN SyncRuns sr
            ON sr.SyncRunId = cc.SyncRunId

        WHERE sr.WaveId = $waveId
          AND cc.ClanId = $clanId;
        """;

        command.Parameters.AddWithValue(
            "$waveId",
            report.WaveId);

        command.Parameters.AddWithValue(
            "$clanId",
            report.ClanId);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            report.ReputationGain =
                reader.GetInt32(0);

            report.ReputationDeduction =
                reader.GetInt32(1);

            report.NetReputation =
                reader.GetInt32(2);
        }
    }

    private static async Task LoadMembersAsync(
        DiscordWaveReportData report,
        int limit,
        SqliteConnection connection)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        WITH Totals AS
        (
            SELECT
                mc.MemberId,

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
                ON sr.SyncRunId =
                    mc.SyncRunId

            WHERE sr.WaveId = $waveId
              AND mc.ClanId = $clanId

            GROUP BY mc.MemberId
        ),

        Ranked AS
        (
            SELECT
                RANK() OVER
                (
                    ORDER BY
                        t.ReputationGain DESC
                ) AS MemberRank,

                t.MemberId,
                t.ReputationGain,
                t.ReputationDeduction,
                t.NetReputation

            FROM Totals t
        )

        SELECT
            r.MemberRank,
            m.MemberId,
            m.Name,
            m.Level,
            r.ReputationGain,
            r.ReputationDeduction,
            r.NetReputation

        FROM Ranked r

        JOIN Members m
            ON m.MemberId = r.MemberId

        ORDER BY
            r.MemberRank,
            m.MemberId

        LIMIT $limit;
        """;

        command.Parameters.AddWithValue(
            "$waveId",
            report.WaveId);

        command.Parameters.AddWithValue(
            "$clanId",
            report.ClanId);

        command.Parameters.AddWithValue(
            "$limit",
            limit);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            report.Members.Add(
                new MemberRankingPanelEntry
                {
                    Rank = reader.GetInt32(0),
                    MemberId = reader.GetInt32(1),
                    MemberName = reader.GetString(2),
                    Level = reader.GetInt32(3),
                    WaveReputationGain =
                        reader.GetInt32(4),
                    WaveReputationDeduction =
                        reader.GetInt32(5),
                    WaveNetReputation =
                        reader.GetInt32(6)
                });
        }
    }

    public async Task MarkPublishedAsync(
        string guildId,
        long waveId,
        string messageId)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        INSERT INTO DiscordWaveReports
        (
            GuildId,
            WaveId,
            MessageId,
            PublishedAt
        )
        VALUES
        (
            $guildId,
            $waveId,
            $messageId,
            $publishedAt
        )
        ON CONFLICT(GuildId, WaveId)
        DO NOTHING;
        """;

        command.Parameters.AddWithValue(
            "$guildId",
            guildId);

        command.Parameters.AddWithValue(
            "$waveId",
            waveId);

        command.Parameters.AddWithValue(
            "$messageId",
            messageId);

        command.Parameters.AddWithValue(
            "$publishedAt",
            DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long?>
        GetCurrentWaveIdAsync(
            SqliteConnection connection)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT WaveId

        FROM SyncRuns

        WHERE WaveId IS NOT NULL

        ORDER BY GeneratedAt DESC

        LIMIT 1;
        """;

        object? result =
            await command.ExecuteScalarAsync();

        if (result is null || result is DBNull)
        {
            return null;
        }

        return Convert.ToInt64(result);
    }
}