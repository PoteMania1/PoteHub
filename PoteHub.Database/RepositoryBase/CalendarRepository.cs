using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class CalendarRepository : RepositoryBase
{
    public CalendarRepository(DatabaseConnection database)
        : base(database)
    {
    }

    public async Task SaveDayAsync(
        Day day,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO Days
        (
            SeasonId,
            DayNumber,
            ServerDate,
            StartTime,
            EndTime,
            IsCompleted
        )
        VALUES
        (
            $seasonId,
            $dayNumber,
            $serverDate,
            $startTime,
            $endTime,
            $isCompleted
        )
        ON CONFLICT(SeasonId, DayNumber) DO UPDATE SET
            ServerDate = excluded.ServerDate,
            StartTime = excluded.StartTime,
            EndTime = excluded.EndTime;
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            day.SeasonId);

        command.Parameters.AddWithValue(
            "$dayNumber",
            day.DayNumber);

        command.Parameters.AddWithValue(
            "$serverDate",
            day.ServerDate.ToString("yyyy-MM-dd"));

        command.Parameters.AddWithValue(
            "$startTime",
            day.StartTime.ToString("O"));

        command.Parameters.AddWithValue(
            "$endTime",
            day.EndTime.ToString("O"));

        command.Parameters.AddWithValue(
            "$isCompleted",
            day.IsCompleted ? 1 : 0);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Day?> GetDayAsync(
        int seasonId,
        int dayNumber,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        SELECT
            DayId,
            SeasonId,
            DayNumber,
            ServerDate,
            StartTime,
            EndTime,
            IsCompleted
        FROM Days
        WHERE SeasonId = $seasonId
          AND DayNumber = $dayNumber;
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        command.Parameters.AddWithValue(
            "$dayNumber",
            dayNumber);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new Day
        {
            DayId = reader.GetInt64(0),
            SeasonId = reader.GetInt32(1),
            DayNumber = reader.GetInt32(2),

            ServerDate = DateTime.Parse(
                reader.GetString(3)),

            StartTime = DateTime.Parse(
                reader.GetString(4)),

            EndTime = DateTime.Parse(
                reader.GetString(5)),

            IsCompleted = reader.GetInt32(6) == 1
        };
    }

    public async Task SaveWaveAsync(
        Wave wave,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO Waves
        (
            DayId,
            SeasonId,
            WaveNumber,
            StartTime,
            EndTime,
            Status,
            SuccessfulSyncCount
        )
        VALUES
        (
            $dayId,
            $seasonId,
            $waveNumber,
            $startTime,
            $endTime,
            $status,
            0
        )
        ON CONFLICT(DayId, WaveNumber) DO UPDATE SET
            StartTime = excluded.StartTime,
            EndTime = excluded.EndTime;
        """;

        command.Parameters.AddWithValue(
            "$dayId",
            wave.DayId);

        command.Parameters.AddWithValue(
            "$seasonId",
            wave.SeasonId);

        command.Parameters.AddWithValue(
            "$waveNumber",
            wave.WaveNumber);

        command.Parameters.AddWithValue(
            "$startTime",
            wave.StartTime.ToString("O"));

        command.Parameters.AddWithValue(
            "$endTime",
            wave.EndTime.ToString("O"));

        command.Parameters.AddWithValue(
            "$status",
            wave.Status);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Wave?> GetWaveAsync(
        long dayId,
        int waveNumber,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        SELECT
            WaveId,
            DayId,
            SeasonId,
            WaveNumber,
            StartTime,
            EndTime,
            Status,
            SuccessfulSyncCount,
            FirstGeneratedAt,
            LastGeneratedAt,
            CompletedAt
        FROM Waves
        WHERE DayId = $dayId
          AND WaveNumber = $waveNumber;
        """;

        command.Parameters.AddWithValue(
            "$dayId",
            dayId);

        command.Parameters.AddWithValue(
            "$waveNumber",
            waveNumber);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new Wave
        {
            WaveId = reader.GetInt64(0),
            DayId = reader.GetInt64(1),
            SeasonId = reader.GetInt32(2),
            WaveNumber = reader.GetInt32(3),
            StartTime = DateTime.Parse(reader.GetString(4)),
            EndTime = DateTime.Parse(reader.GetString(5)),
            Status = reader.GetString(6),
            SuccessfulSyncCount = reader.GetInt32(7),

            FirstGeneratedAt = reader.IsDBNull(8)
                ? null
                : DateTime.Parse(reader.GetString(8)),

            LastGeneratedAt = reader.IsDBNull(9)
                ? null
                : DateTime.Parse(reader.GetString(9)),

            CompletedAt = reader.IsDBNull(10)
                ? null
                : DateTime.Parse(reader.GetString(10))
        };
    }

    public async Task RegisterSuccessfulSyncAsync(
        long waveId,
        DateTime generatedAt,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        UPDATE Waves
        SET Status = 'InProgress',
            SuccessfulSyncCount =
                SuccessfulSyncCount + 1,

            FirstGeneratedAt =
                COALESCE(
                    FirstGeneratedAt,
                    $generatedAt),

            LastGeneratedAt = $generatedAt
        WHERE WaveId = $waveId;
        """;

        command.Parameters.AddWithValue(
            "$waveId",
            waveId);

        command.Parameters.AddWithValue(
            "$generatedAt",
            generatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task FinalizePastWavesAsync(
        int seasonId,
        DateTime currentGeneratedAt,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        UPDATE Waves
        SET Status =
            CASE
                WHEN SuccessfulSyncCount = 0
                    THEN 'NoData'

                WHEN SuccessfulSyncCount = 1
                    THEN 'Incomplete'

                ELSE 'Complete'
            END,

            CompletedAt = $completedAt
        WHERE SeasonId = $seasonId
          AND EndTime <= $currentGeneratedAt
          AND Status IN ('Pending', 'InProgress');
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        command.Parameters.AddWithValue(
            "$currentGeneratedAt",
            currentGeneratedAt.ToString("O"));

        command.Parameters.AddWithValue(
            "$completedAt",
            DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }
}