using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;
using System.Globalization;

namespace PoteHub.Database.RepositoryBase;

public class AttackCaptureRepository
    : RepositoryBase
{
    public AttackCaptureRepository(
        DatabaseConnection database)
        : base(database)
    {
    }

    public async Task<AttackCaptureSession> StartAsync(
        string guildId,
        string channelId,
        string requestedByDiscordId,
        int clanId,
        int waveCount)
    {
        using SqliteConnection connection =
            CreateConnection();

        await connection.OpenAsync();

        using SqliteTransaction transaction =
            connection.BeginTransaction();

        try
        {
            string? clanName =
                await GetClanNameAsync(
                    clanId,
                    connection,
                    transaction);

            if (clanName is null)
            {
                throw new ArgumentException(
                    $"No existe un clan con ID {clanId}.");
            }

            bool alreadyActive =
                await HasActiveSessionAsync(
                    guildId,
                    channelId,
                    connection,
                    transaction);

            if (alreadyActive)
            {
                throw new InvalidOperationException(
                    "Ya hay un registro activo en este canal.");
            }

            AttackCaptureSession? context =
                await GetCurrentContextAsync(
                    connection,
                    transaction);

            if (context is null)
            {
                throw new InvalidOperationException(
                    "Todavía no existe una wave sincronizada.");
            }

            int availableWaves =
                await CountAvailableWavesAsync(
                    context.SeasonId,
                    context.StartWaveId,
                    waveCount,
                    connection,
                    transaction);

            if (availableWaves < waveCount)
            {
                throw new InvalidOperationException(
                    "No quedan suficientes waves en esta " +
                    "temporada para completar el registro.");
            }

            context.GuildId = guildId;
            context.ChannelId = channelId;
            context.RequestedByDiscordId =
                requestedByDiscordId;

            context.ClanId = clanId;
            context.ClanName = clanName;
            context.WaveCount = waveCount;
            context.StartedAt = DateTime.UtcNow;
            context.Status = "Active";

            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
            """
            INSERT INTO AttackCaptureSessions
            (
                GuildId,
                ChannelId,
                RequestedByDiscordId,
                ClanId,
                SeasonId,
                StartWaveId,
                StartSyncRunId,
                WaveCount,
                StartedAt,
                Status
            )
            VALUES
            (
                $guildId,
                $channelId,
                $requestedByDiscordId,
                $clanId,
                $seasonId,
                $startWaveId,
                $startSyncRunId,
                $waveCount,
                $startedAt,
                'Active'
            );

            SELECT last_insert_rowid();
            """;

            command.Parameters.AddWithValue(
                "$guildId",
                guildId);

            command.Parameters.AddWithValue(
                "$channelId",
                channelId);

            command.Parameters.AddWithValue(
                "$requestedByDiscordId",
                requestedByDiscordId);

            command.Parameters.AddWithValue(
                "$clanId",
                clanId);

            command.Parameters.AddWithValue(
                "$seasonId",
                context.SeasonId);

            command.Parameters.AddWithValue(
                "$startWaveId",
                context.StartWaveId);

            command.Parameters.AddWithValue(
                "$startSyncRunId",
                context.StartSyncRunId);

            command.Parameters.AddWithValue(
                "$waveCount",
                waveCount);

            command.Parameters.AddWithValue(
                "$startedAt",
                context.StartedAt.ToString("O"));

            object? result =
                await command.ExecuteScalarAsync();

            context.SessionId =
                Convert.ToInt64(result);

            await transaction.CommitAsync();

            return context;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<AttackCaptureSession>>
        GetActiveAsync()
    {
        List<AttackCaptureSession> sessions = [];

        using SqliteConnection connection =
            CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            acs.SessionId,
            acs.GuildId,
            acs.ChannelId,
            acs.RequestedByDiscordId,
            acs.ClanId,
            c.Name,
            acs.SeasonId,
            s.Name,
            acs.StartWaveId,
            acs.StartSyncRunId,
            acs.WaveCount,
            acs.StartedAt,
            acs.Status

        FROM AttackCaptureSessions acs

        JOIN Clans c
            ON c.ClanId = acs.ClanId

        JOIN Seasons s
            ON s.SeasonId = acs.SeasonId

        WHERE acs.Status = 'Active';
        """;

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            sessions.Add(
                new AttackCaptureSession
                {
                    SessionId = reader.GetInt64(0),
                    GuildId = reader.GetString(1),
                    ChannelId = reader.GetString(2),

                    RequestedByDiscordId =
                        reader.GetString(3),

                    ClanId = reader.GetInt32(4),
                    ClanName = reader.GetString(5),
                    SeasonId = reader.GetInt32(6),
                    SeasonName = reader.GetString(7),
                    StartWaveId = reader.GetInt64(8),
                    StartSyncRunId = reader.GetInt64(9),
                    WaveCount = reader.GetInt32(10),

                    StartedAt = DateTime.Parse(
                        reader.GetString(11),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind),

                    Status = reader.GetString(12)
                });
        }

        return sessions;
    }

    public async Task<bool> IsFinishedAsync(
    AttackCaptureSession session)
    {
        using SqliteConnection connection =
            CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
    WITH TargetWave AS
    (
        SELECT
            WaveId,
            EndTime

        FROM Waves

        WHERE SeasonId = $seasonId
          AND WaveId >= $startWaveId

        ORDER BY WaveId

        LIMIT 1 OFFSET $offset
    )

    SELECT
        tw.EndTime,

        (
            SELECT sr.GeneratedAt

            FROM SyncRuns sr

            WHERE sr.SyncRunId > $startSyncRunId
              AND sr.GeneratedAt IS NOT NULL

            ORDER BY sr.SyncRunId DESC

            LIMIT 1
        ) AS LatestGeneratedAt

    FROM TargetWave tw;
    """;

        command.Parameters.AddWithValue(
            "$seasonId",
            session.SeasonId);

        command.Parameters.AddWithValue(
            "$startWaveId",
            session.StartWaveId);

        command.Parameters.AddWithValue(
            "$startSyncRunId",
            session.StartSyncRunId);

        command.Parameters.AddWithValue(
            "$offset",
            session.WaveCount - 1);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return false;
        }

        if (reader.IsDBNull(1))
        {
            return false;
        }

        DateTime targetEndTime =
            DateTime.Parse(
                reader.GetString(0),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);

        DateTime latestGeneratedAt =
            DateTime.Parse(
                reader.GetString(1),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);

        return latestGeneratedAt >= targetEndTime;
    }

    public async Task<List<AttackCaptureEntry>>
        GetEntriesAsync(
            AttackCaptureSession session)
    {
        List<AttackCaptureEntry> entries = [];

        using SqliteConnection connection =
            CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        WITH TargetEnd AS
        (
            SELECT EndTime
        
            FROM Waves
        
            WHERE SeasonId = $seasonId
              AND WaveId >= $startWaveId
        
            ORDER BY WaveId
        
            LIMIT 1 OFFSET $waveOffset
        )
        
        SELECT
            w.WaveId,
            d.DayNumber,
            w.WaveNumber,
            w.StartTime,
            m.MemberId,
            m.Name,
            mc.ReputationDifference,
            sr.GeneratedAt
        
        FROM MemberChanges mc
        
        JOIN SyncRuns sr
            ON sr.SyncRunId = mc.SyncRunId
        
        JOIN Waves w
            ON w.WaveId = sr.WaveId
        
        JOIN Days d
            ON d.DayId = w.DayId
        
        JOIN Members m
            ON m.MemberId = mc.MemberId
        
        CROSS JOIN TargetEnd target
        
        WHERE mc.ClanId = $clanId
          AND mc.SeasonId = $seasonId
          AND mc.DetectedAt >= $startedAt
          AND mc.ReputationDifference > 0
          AND sr.GeneratedAt < target.EndTime
        
        ORDER BY
            sr.GeneratedAt,
            mc.MemberChangeId;
        """;

        command.Parameters.AddWithValue(
            "$clanId",
            session.ClanId);

        command.Parameters.AddWithValue(
            "$seasonId",
            session.SeasonId);

        command.Parameters.AddWithValue(
            "$startWaveId",
            session.StartWaveId);

        command.Parameters.AddWithValue(
            "$startedAt",
            session.StartedAt.ToString("O"));

        command.Parameters.AddWithValue(
            "$waveOffset",
            session.WaveCount - 1);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            entries.Add(
                new AttackCaptureEntry
                {
                    WaveId = reader.GetInt64(0),
                    DayNumber = reader.GetInt32(1),
                    WaveNumber = reader.GetInt32(2),

                    WaveStartTime = DateTime.Parse(
                    reader.GetString(3),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),

                    MemberId = reader.GetInt32(4),
                    MemberName = reader.GetString(5),

                    ReputationAmount =
                    reader.GetInt32(6),

                    DetectedAt = DateTime.Parse(
                    reader.GetString(7),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind)
                });
        }

        return entries;
    }

    public async Task MarkCompletedAsync(
        long sessionId)
    {
        await ChangeStatusAsync(
            sessionId,
            "Completed");
    }

    public async Task<bool> CancelAsync(
        string guildId,
        string channelId)
    {
        using SqliteConnection connection =
            CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        UPDATE AttackCaptureSessions

        SET
            Status = 'Cancelled',
            CompletedAt = $completedAt

        WHERE GuildId = $guildId
          AND ChannelId = $channelId
          AND Status = 'Active';
        """;

        command.Parameters.AddWithValue(
            "$guildId",
            guildId);

        command.Parameters.AddWithValue(
            "$channelId",
            channelId);

        command.Parameters.AddWithValue(
            "$completedAt",
            DateTime.UtcNow.ToString("O"));

        return await command.ExecuteNonQueryAsync() > 0;
    }

    private async Task ChangeStatusAsync(
        long sessionId,
        string status)
    {
        using SqliteConnection connection =
            CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        UPDATE AttackCaptureSessions

        SET
            Status = $status,
            CompletedAt = $completedAt

        WHERE SessionId = $sessionId
          AND Status = 'Active';
        """;

        command.Parameters.AddWithValue(
            "$status",
            status);

        command.Parameters.AddWithValue(
            "$completedAt",
            DateTime.UtcNow.ToString("O"));

        command.Parameters.AddWithValue(
            "$sessionId",
            sessionId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> GetClanNameAsync(
        int clanId,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        SELECT Name
        FROM Clans
        WHERE ClanId = $clanId;
        """;

        command.Parameters.AddWithValue(
            "$clanId",
            clanId);

        return Convert.ToString(
            await command.ExecuteScalarAsync());
    }

    private static async Task<bool>
        HasActiveSessionAsync(
            string guildId,
            string channelId,
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

            FROM AttackCaptureSessions

            WHERE GuildId = $guildId
              AND ChannelId = $channelId
              AND Status = 'Active'
        );
        """;

        command.Parameters.AddWithValue(
            "$guildId",
            guildId);

        command.Parameters.AddWithValue(
            "$channelId",
            channelId);

        return Convert.ToInt32(
            await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task<AttackCaptureSession?>
        GetCurrentContextAsync(
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        SELECT
            sr.SyncRunId,
            sr.SeasonId,
            sr.WaveId,
            s.Name,
            d.DayNumber,
            w.WaveNumber

        FROM SyncRuns sr

        JOIN Seasons s
            ON s.SeasonId = sr.SeasonId

        JOIN Waves w
            ON w.WaveId = sr.WaveId

        JOIN Days d
            ON d.DayId = w.DayId

        WHERE sr.WaveId IS NOT NULL

        ORDER BY sr.SyncRunId DESC

        LIMIT 1;
        """;

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new AttackCaptureSession
        {
            StartSyncRunId = reader.GetInt64(0),
            SeasonId = reader.GetInt32(1),
            StartWaveId = reader.GetInt64(2),
            SeasonName = reader.GetString(3),
            StartDayNumber = reader.GetInt32(4),
            StartWaveNumber = reader.GetInt32(5)
        };
    }

    private static async Task<int>
        CountAvailableWavesAsync(
            int seasonId,
            long startWaveId,
            int waveCount,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        SELECT COUNT(*)

        FROM
        (
            SELECT WaveId

            FROM Waves

            WHERE SeasonId = $seasonId
              AND WaveId >= $startWaveId

            ORDER BY WaveId

            LIMIT $waveCount
        );
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        command.Parameters.AddWithValue(
            "$startWaveId",
            startWaveId);

        command.Parameters.AddWithValue(
            "$waveCount",
            waveCount);

        return Convert.ToInt32(
            await command.ExecuteScalarAsync());
    }
}