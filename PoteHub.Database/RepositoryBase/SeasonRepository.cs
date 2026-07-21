using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class SeasonRepository : RepositoryBase
{
    public SeasonRepository(DatabaseConnection database)
        : base(database)
    {
    }

    public async Task SaveAsync(
        Season season,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO Seasons
        (
            SeasonId,
            Name,
            StartTime,
            EndTime,
            EndTimeTimestamp,
            IsCompleted
        )
        VALUES
        (
            $id,
            $name,
            $start,
            $end,
            $timestamp,
            $completed
        )
        ON CONFLICT(SeasonId) DO UPDATE SET
            Name = excluded.Name,
            StartTime = excluded.StartTime,
            EndTime = excluded.EndTime,
            EndTimeTimestamp = excluded.EndTimeTimestamp,
            IsCompleted = excluded.IsCompleted;
        """;

        command.Parameters.AddWithValue("$id", season.SeasonId);
        command.Parameters.AddWithValue("$name", season.Name);
        command.Parameters.AddWithValue(
            "$start",
            season.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));

        command.Parameters.AddWithValue(
            "$end",
            season.EndTime.ToString("yyyy-MM-dd HH:mm:ss"));

        command.Parameters.AddWithValue("$timestamp", season.EndTimeTimestamp);
        command.Parameters.AddWithValue("$completed", season.IsCompleted ? 1 : 0);

        await command.ExecuteNonQueryAsync();
    }
}