using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class DiscordPanelRepository
    : RepositoryBase
{
    public DiscordPanelRepository(
        DatabaseConnection database)
        : base(database)
    {
    }

    public async Task<string?> GetClanNameAsync(
        int clanId)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT Name
        FROM Clans
        WHERE ClanId = $clanId
        LIMIT 1;
        """;

        command.Parameters.AddWithValue(
            "$clanId",
            clanId);

        object? result =
            await command.ExecuteScalarAsync();

        return result?.ToString();
    }

    public async Task ConfigureAsync(
        string guildId,
        string panelType,
        string channelId,
        int? clanId)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteTransaction transaction =
            connection.BeginTransaction();

        try
        {
            string timestamp =
                DateTime.UtcNow.ToString("O");

            await SaveGuildSettingsAsync(
                guildId,
                panelType,
                channelId,
                clanId,
                timestamp,
                connection,
                transaction);

            await SavePanelAsync(
                guildId,
                panelType,
                channelId,
                clanId,
                timestamp,
                connection,
                transaction);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task
        SaveGuildSettingsAsync(
            string guildId,
            string panelType,
            string channelId,
            int? clanId,
            string timestamp,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand insertCommand =
            connection.CreateCommand();

        insertCommand.Transaction = transaction;

        insertCommand.CommandText =
        """
        INSERT INTO DiscordGuildSettings
        (
            GuildId,
            UpdatedAt
        )
        VALUES
        (
            $guildId,
            $updatedAt
        )
        ON CONFLICT(GuildId) DO UPDATE SET
            UpdatedAt = excluded.UpdatedAt;
        """;

        insertCommand.Parameters.AddWithValue(
            "$guildId",
            guildId);

        insertCommand.Parameters.AddWithValue(
            "$updatedAt",
            timestamp);

        await insertCommand.ExecuteNonQueryAsync();

        using SqliteCommand updateCommand =
            connection.CreateCommand();

        updateCommand.Transaction = transaction;

        if (panelType == "ClanRanking")
        {
            updateCommand.CommandText =
            """
            UPDATE DiscordGuildSettings
            SET
                ClanRankingChannelId =
                    $channelId,

                UpdatedAt =
                    $updatedAt

            WHERE GuildId = $guildId;
            """;
        }
        else
        {
            updateCommand.CommandText =
            """
            UPDATE DiscordGuildSettings
            SET
                MemberRankingChannelId =
                    $channelId,

                HomeClanId =
                    $clanId,

                UpdatedAt =
                    $updatedAt

            WHERE GuildId = $guildId;
            """;

            updateCommand.Parameters.AddWithValue(
                "$clanId",
                clanId!.Value);
        }

        updateCommand.Parameters.AddWithValue(
            "$guildId",
            guildId);

        updateCommand.Parameters.AddWithValue(
            "$channelId",
            channelId);

        updateCommand.Parameters.AddWithValue(
            "$updatedAt",
            timestamp);

        await updateCommand.ExecuteNonQueryAsync();
    }

    private static async Task SavePanelAsync(
        string guildId,
        string panelType,
        string channelId,
        int? clanId,
        string timestamp,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO DiscordPanels
        (
            GuildId,
            PanelType,
            ChannelId,
            ClanId,
            IsActive,
            CreatedAt,
            UpdatedAt
        )
        VALUES
        (
            $guildId,
            $panelType,
            $channelId,
            $clanId,
            1,
            $createdAt,
            $updatedAt
        )
        ON CONFLICT(GuildId, PanelType)
        DO UPDATE SET
            ChannelId = excluded.ChannelId,
            ClanId = excluded.ClanId,
            IsActive = 1,
            UpdatedAt = excluded.UpdatedAt;
        """;

        command.Parameters.AddWithValue(
            "$guildId",
            guildId);

        command.Parameters.AddWithValue(
            "$panelType",
            panelType);

        command.Parameters.AddWithValue(
            "$channelId",
            channelId);

        command.Parameters.AddWithValue(
            "$clanId",
            clanId is null
                ? DBNull.Value
                : clanId.Value);

        command.Parameters.AddWithValue(
            "$createdAt",
            timestamp);

        command.Parameters.AddWithValue(
            "$updatedAt",
            timestamp);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<DiscordPanel>>
        GetActiveAsync()
    {
        List<DiscordPanel> panels = [];

        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            PanelId,
            GuildId,
            PanelType,
            ChannelId,
            ClanId,
            IsActive,
            CreatedAt,
            UpdatedAt

        FROM DiscordPanels

        WHERE IsActive = 1

        ORDER BY
            GuildId,
            PanelType;
        """;

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            panels.Add(new DiscordPanel
            {
                PanelId = reader.GetInt64(0),
                GuildId = reader.GetString(1),
                PanelType = reader.GetString(2),
                ChannelId = reader.GetString(3),

                ClanId = reader.IsDBNull(4)
                    ? null
                    : reader.GetInt32(4),

                IsActive =
                    reader.GetInt32(5) == 1,

                CreatedAt =
                    DateTime.Parse(
                        reader.GetString(6)),

                UpdatedAt =
                    DateTime.Parse(
                        reader.GetString(7))
            });
        }

        return panels;
    }

    public async Task<ClanRankingPanelData?>
    GetCurrentClanRankingAsync(
        int limit)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        ClanRankingPanelData? data =
            await GetCurrentWaveAsync(
                connection);

        if (data is null)
        {
            return null;
        }

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
    WITH WaveTotals AS
    (
        SELECT
            cc.ClanId,

            SUM(
                cc.ReputationDifference
            ) AS WaveReputation,

            SUM
            (
                cc.CurrentDeduction -
                cc.PreviousDeduction
            ) AS WaveDeduction

        FROM ClanChanges cc

        JOIN SyncRuns sr
            ON sr.SyncRunId =
                cc.SyncRunId

        WHERE sr.WaveId = $waveId

        GROUP BY
            cc.ClanId
    )

    SELECT
        cs.Rank,
        c.ClanId,
        c.Name,
        cs.Reputation,
        cs.Deduction,

        COALESCE(
            wt.WaveReputation,
            0
        ),

        COALESCE(
            wt.WaveDeduction,
            0
        )

    FROM ClanSeasons cs

    JOIN Clans c
        ON c.ClanId = cs.ClanId

    LEFT JOIN WaveTotals wt
        ON wt.ClanId = cs.ClanId

    WHERE cs.SeasonId = $seasonId

    ORDER BY
        cs.Rank ASC,
        c.ClanId ASC

    LIMIT $limit;
    """;

        command.Parameters.AddWithValue(
            "$waveId",
            data.WaveId);

        command.Parameters.AddWithValue(
            "$seasonId",
            data.SeasonId);

        command.Parameters.AddWithValue(
            "$limit",
            limit);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            data.Clans.Add(
                new ClanRankingPanelEntry
                {
                    Rank = reader.GetInt32(0),
                    ClanId = reader.GetInt32(1),
                    ClanName = reader.GetString(2),
                    TotalReputation =
                        reader.GetInt32(3),
                    TotalDeduction =
                        reader.GetInt32(4),
                    WaveReputation =
                        reader.GetInt32(5),
                    WaveDeduction =
                        reader.GetInt32(6)
                });
        }

        return data;
    }

    private static async Task
        <ClanRankingPanelData?>
        GetCurrentWaveAsync(
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
        sr.GeneratedAt

    FROM SyncRuns sr

    JOIN Seasons s
        ON s.SeasonId = sr.SeasonId

    JOIN Days d
        ON d.DayId = sr.DayId

    JOIN Waves w
        ON w.WaveId = sr.WaveId

    WHERE sr.DayId IS NOT NULL
      AND sr.WaveId IS NOT NULL
      AND sr.GeneratedAt IS NOT NULL

    ORDER BY
        sr.GeneratedAt DESC

    LIMIT 1;
    """;

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new ClanRankingPanelData
        {
            SeasonId = reader.GetInt32(0),
            SeasonName = reader.GetString(1),
            DayId = reader.GetInt64(2),
            DayNumber = reader.GetInt32(3),
            WaveId = reader.GetInt64(4),
            WaveNumber = reader.GetInt32(5),

            WaveStartTime =
                DateTime.Parse(
                    reader.GetString(6)),

            WaveEndTime =
                DateTime.Parse(
                    reader.GetString(7)),

            WaveStatus =
                reader.GetString(8),

            LastUpdatedAt =
                DateTime.Parse(
                    reader.GetString(9))
        };
    }

    public async Task<DiscordPanelMessage?>
    GetCurrentMessageAsync(
        long panelId)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
    SELECT
        PanelMessageId,
        PanelId,
        SeasonId,
        DayId,
        WaveId,
        MessageId,
        IsCurrent,
        CreatedAt,
        FinalizedAt

    FROM DiscordPanelMessages

    WHERE PanelId = $panelId
      AND IsCurrent = 1

    LIMIT 1;
    """;

        command.Parameters.AddWithValue(
            "$panelId",
            panelId);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return ReadPanelMessage(reader);
    }

    public async Task<DiscordPanelMessage?>
    GetMessageAsync(
        long panelId,
        long waveId)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
    SELECT
        PanelMessageId,
        PanelId,
        SeasonId,
        DayId,
        WaveId,
        MessageId,
        IsCurrent,
        CreatedAt,
        FinalizedAt

    FROM DiscordPanelMessages

    WHERE PanelId = $panelId
      AND WaveId = $waveId

    LIMIT 1;
    """;

        command.Parameters.AddWithValue(
            "$panelId",
            panelId);

        command.Parameters.AddWithValue(
            "$waveId",
            waveId);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return ReadPanelMessage(reader);
    }

    private static DiscordPanelMessage
    ReadPanelMessage(
        SqliteDataReader reader)
    {
        return new DiscordPanelMessage
        {
            PanelMessageId =
                reader.GetInt64(0),

            PanelId =
                reader.GetInt64(1),

            SeasonId =
                reader.GetInt32(2),

            DayId =
                reader.GetInt64(3),

            WaveId =
                reader.GetInt64(4),

            MessageId =
                reader.GetString(5),

            IsCurrent =
                reader.GetInt32(6) == 1,

            CreatedAt =
                DateTime.Parse(
                    reader.GetString(7)),

            FinalizedAt =
                reader.IsDBNull(8)
                    ? null
                    : DateTime.Parse(
                        reader.GetString(8))
        };
    }

    public async Task SaveCurrentMessageAsync(
        long panelId,
        int seasonId,
        long dayId,
        long waveId,
        string messageId)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteTransaction transaction =
            connection.BeginTransaction();

        try
        {
            using SqliteCommand finalizeCommand =
                connection.CreateCommand();

            finalizeCommand.Transaction =
                transaction;

            finalizeCommand.CommandText =
            """
            UPDATE DiscordPanelMessages
            SET
                IsCurrent = 0,
                FinalizedAt = $finalizedAt
            WHERE PanelId = $panelId
              AND IsCurrent = 1
              AND WaveId <> $waveId;
            """;

            finalizeCommand.Parameters.AddWithValue(
                "$panelId",
                panelId);

            finalizeCommand.Parameters.AddWithValue(
                "$waveId",
                waveId);

            finalizeCommand.Parameters.AddWithValue(
                "$finalizedAt",
                DateTime.UtcNow.ToString("O"));

            await finalizeCommand
                .ExecuteNonQueryAsync();

            using SqliteCommand saveCommand =
                connection.CreateCommand();

            saveCommand.Transaction = transaction;

            saveCommand.CommandText =
            """
            INSERT INTO DiscordPanelMessages
            (
                PanelId,
                SeasonId,
                DayId,
                WaveId,
                MessageId,
                IsCurrent,
                CreatedAt
            )
            VALUES
            (
                $panelId,
                $seasonId,
                $dayId,
                $waveId,
                $messageId,
                1,
                $createdAt
            )
            ON CONFLICT(PanelId, WaveId)
            DO UPDATE SET
                MessageId = excluded.MessageId,
                IsCurrent = 1,
                FinalizedAt = NULL;
            """;

            saveCommand.Parameters.AddWithValue(
                "$panelId",
                panelId);

            saveCommand.Parameters.AddWithValue(
                "$seasonId",
                seasonId);

            saveCommand.Parameters.AddWithValue(
                "$dayId",
                dayId);

            saveCommand.Parameters.AddWithValue(
                "$waveId",
                waveId);

            saveCommand.Parameters.AddWithValue(
                "$messageId",
                messageId);

            saveCommand.Parameters.AddWithValue(
                "$createdAt",
                DateTime.UtcNow.ToString("O"));

            await saveCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}