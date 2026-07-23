using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;

namespace PoteHub.Database.RepositoryBase;

public class DataRetentionRepository
    : RepositoryBase
{
    private const int DetailedDaysToKeep = 5;
    private const int FinalSeasonDaysToKeep = 2;

    public DataRetentionRepository(
        DatabaseConnection database)
        : base(database)
    {
    }

    public async Task<RetentionResult> RunAsync(
        int currentSeasonId,
        long currentDayId,
        int currentDayNumber,
        DateTime generatedAt,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        RetentionResult result = new();

        string dailyKey =
            $"DailyRetention:{currentSeasonId}";

        string? lastProcessedDay =
            await GetStateAsync(
                dailyKey,
                connection,
                transaction);

        string currentDayValue =
            currentDayId.ToString();

        if (lastProcessedDay != currentDayValue)
        {
            result.DailyMaintenanceRan = true;

            long? previousDayId =
                await GetPreviousDayIdAsync(
                    currentSeasonId,
                    currentDayNumber,
                    connection,
                    transaction);

            if (previousDayId is not null)
            {
                await CreateDailySummariesAsync(
                    previousDayId.Value,
                    connection,
                    transaction);

                result.SummarizedDays++;

                await MarkDayCompletedAsync(
                    previousDayId.Value,
                    connection,
                    transaction);
            }

            int firstDetailedDayToKeep =
                Math.Max(
                    1,
                    currentDayNumber -
                    DetailedDaysToKeep + 1);

            result.DeletedMemberChanges +=
                await DeleteOldMemberChangesAsync(
                    currentSeasonId,
                    firstDetailedDayToKeep,
                    connection,
                    transaction);

            result.DeletedClanChanges +=
                await DeleteOldClanChangesAsync(
                    currentSeasonId,
                    firstDetailedDayToKeep,
                    connection,
                    transaction);

            result.DeletedSyncRuns +=
                await DeleteOldOrphanSyncRunsAsync(
                    currentSeasonId,
                    firstDetailedDayToKeep,
                    connection,
                    transaction);

            await SaveStateAsync(
                dailyKey,
                currentDayValue,
                connection,
                transaction);
        }

        result.FinalizedSeasons =
            await FinalizeEndedSeasonsAsync(
                generatedAt,
                result,
                connection,
                transaction);

        return result;
    }

    private static async Task<long?>
        GetPreviousDayIdAsync(
            int seasonId,
            int currentDayNumber,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        SELECT DayId
        FROM Days
        WHERE SeasonId = $seasonId
          AND DayNumber < $currentDayNumber
        ORDER BY DayNumber DESC
        LIMIT 1;
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        command.Parameters.AddWithValue(
            "$currentDayNumber",
            currentDayNumber);

        object? value =
            await command.ExecuteScalarAsync();

        return value is null ||
               value == DBNull.Value
            ? null
            : Convert.ToInt64(value);
    }

    private static async Task
        CreateDailySummariesAsync(
            long dayId,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        await CreateClanSummariesAsync(
            dayId,
            connection,
            transaction);

        await CreateMemberSummariesAsync(
            dayId,
            connection,
            transaction);

        await CalculateDailyRanksAsync(
            dayId,
            connection,
            transaction);
    }

    private static async Task
        CreateClanSummariesAsync(
            long dayId,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT OR REPLACE INTO ClanDailySummaries
        (
            DayId,
            SeasonId,
            ClanId,
            ReputationStart,
            ReputationEnd,
            ReputationGain,
            ReputationDeduction,
            RankStart,
            RankEnd,
            CreatedAt
        )
        SELECT
            d.DayId,
            d.SeasonId,
            cs.ClanId,

            COALESCE
            (
                (
                    SELECT cc.PreviousReputation
                    FROM ClanChanges cc
                    JOIN SyncRuns sr
                        ON sr.SyncRunId =
                           cc.SyncRunId
                    WHERE sr.DayId = d.DayId
                      AND cc.ClanId = cs.ClanId
                    ORDER BY
                        sr.GeneratedAt,
                        cc.ClanChangeId
                    LIMIT 1
                ),
                cs.Reputation
            ),

            cs.Reputation,

            COALESCE
            (
                (
                    SELECT SUM
                    (
                        CASE
                            WHEN cc.ReputationDifference > 0
                            THEN cc.ReputationDifference
                            ELSE 0
                        END
                    )
                    FROM ClanChanges cc
                    JOIN SyncRuns sr
                        ON sr.SyncRunId =
                           cc.SyncRunId
                    WHERE sr.DayId = d.DayId
                      AND cc.ClanId = cs.ClanId
                ),
                0
            ),

            COALESCE
            (
                (
                    SELECT SUM
                    (
                        CASE
                            WHEN
                                cc.CurrentDeduction >
                                cc.PreviousDeduction
                            THEN
                                cc.CurrentDeduction -
                                cc.PreviousDeduction
                            ELSE 0
                        END
                    )
                    FROM ClanChanges cc
                    JOIN SyncRuns sr
                        ON sr.SyncRunId =
                           cc.SyncRunId
                    WHERE sr.DayId = d.DayId
                      AND cc.ClanId = cs.ClanId
                ),
                0
            ),

            COALESCE
            (
                (
                    SELECT cc.PreviousRank
                    FROM ClanChanges cc
                    JOIN SyncRuns sr
                        ON sr.SyncRunId =
                           cc.SyncRunId
                    WHERE sr.DayId = d.DayId
                      AND cc.ClanId = cs.ClanId
                    ORDER BY
                        sr.GeneratedAt,
                        cc.ClanChangeId
                    LIMIT 1
                ),
                cs.Rank
            ),

            cs.Rank,
            $createdAt

        FROM Days d

        JOIN ClanSeasons cs
            ON cs.SeasonId = d.SeasonId

        WHERE d.DayId = $dayId;
        """;

        command.Parameters.AddWithValue(
            "$dayId",
            dayId);

        command.Parameters.AddWithValue(
            "$createdAt",
            DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    private static async Task
        CreateMemberSummariesAsync(
            long dayId,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT OR REPLACE INTO MemberDailySummaries
        (
            DayId,
            SeasonId,
            MemberId,
            ClanId,
            ReputationStart,
            ReputationEnd,
            ReputationGain,
            ReputationDeduction,
            ActiveChanges,
            DailyRank,
            CreatedAt
        )
        SELECT
            d.DayId,
            d.SeasonId,
            mp.MemberId,
            mp.ClanId,

            COALESCE
            (
                (
                    SELECT mc.PreviousReputation
                    FROM MemberChanges mc
                    JOIN SyncRuns sr
                        ON sr.SyncRunId =
                           mc.SyncRunId
                    WHERE sr.DayId = d.DayId
                      AND mc.MemberId = mp.MemberId
                      AND mc.ClanId = mp.ClanId
                    ORDER BY
                        sr.GeneratedAt,
                        mc.MemberChangeId
                    LIMIT 1
                ),
                mp.Reputation
            ),

            mp.Reputation,

            COALESCE
            (
                (
                    SELECT SUM
                    (
                        CASE
                            WHEN mc.ReputationDifference > 0
                            THEN mc.ReputationDifference
                            ELSE 0
                        END
                    )
                    FROM MemberChanges mc
                    JOIN SyncRuns sr
                        ON sr.SyncRunId =
                           mc.SyncRunId
                    WHERE sr.DayId = d.DayId
                      AND mc.MemberId = mp.MemberId
                      AND mc.ClanId = mp.ClanId
                ),
                0
            ),

            COALESCE
            (
                (
                    SELECT SUM
                    (
                        CASE
                            WHEN mc.ReputationDifference < 0
                            THEN -mc.ReputationDifference
                            ELSE 0
                        END
                    )
                    FROM MemberChanges mc
                    JOIN SyncRuns sr
                        ON sr.SyncRunId =
                           mc.SyncRunId
                    WHERE sr.DayId = d.DayId
                      AND mc.MemberId = mp.MemberId
                      AND mc.ClanId = mp.ClanId
                ),
                0
            ),

            COALESCE
            (
                (
                    SELECT COUNT(*)
                    FROM MemberChanges mc
                    JOIN SyncRuns sr
                        ON sr.SyncRunId =
                           mc.SyncRunId
                    WHERE sr.DayId = d.DayId
                      AND mc.MemberId = mp.MemberId
                      AND mc.ClanId = mp.ClanId
                      AND mc.ReputationDifference != 0
                ),
                0
            ),

            NULL,
            $createdAt

        FROM Days d

        JOIN MemberParticipations mp
            ON mp.SeasonId = d.SeasonId

        WHERE d.DayId = $dayId;
        """;

        command.Parameters.AddWithValue(
            "$dayId",
            dayId);

        command.Parameters.AddWithValue(
            "$createdAt",
            DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    private static async Task
        CalculateDailyRanksAsync(
            long dayId,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        WITH Rankings AS
        (
            SELECT
                MemberId,
                ClanId,

                DENSE_RANK() OVER
                (
                    ORDER BY
                        ReputationGain DESC
                ) AS Position

            FROM MemberDailySummaries
            WHERE DayId = $dayId
        )

        UPDATE MemberDailySummaries

        SET DailyRank =
        (
            SELECT r.Position
            FROM Rankings r
            WHERE
                r.MemberId =
                    MemberDailySummaries.MemberId
                AND
                r.ClanId =
                    MemberDailySummaries.ClanId
        )

        WHERE DayId = $dayId;
        """;

        command.Parameters.AddWithValue(
            "$dayId",
            dayId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task
        MarkDayCompletedAsync(
            long dayId,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        UPDATE Days
        SET IsCompleted = 1
        WHERE DayId = $dayId;
        """;

        command.Parameters.AddWithValue(
            "$dayId",
            dayId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int>
        DeleteOldMemberChangesAsync(
            int seasonId,
            int firstDayToKeep,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        DELETE FROM MemberChanges
        WHERE SeasonId = $seasonId
          AND SyncRunId IN
          (
              SELECT sr.SyncRunId
              FROM SyncRuns sr
              JOIN Days d
                  ON d.DayId = sr.DayId
              WHERE d.SeasonId = $seasonId
                AND d.DayNumber < $firstDayToKeep
          );
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        command.Parameters.AddWithValue(
            "$firstDayToKeep",
            firstDayToKeep);

        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<int>
        DeleteOldClanChangesAsync(
            int seasonId,
            int firstDayToKeep,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        DELETE FROM ClanChanges
        WHERE SeasonId = $seasonId
          AND SyncRunId IN
          (
              SELECT sr.SyncRunId
              FROM SyncRuns sr
              JOIN Days d
                  ON d.DayId = sr.DayId
              WHERE d.SeasonId = $seasonId
                AND d.DayNumber < $firstDayToKeep
          );
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        command.Parameters.AddWithValue(
            "$firstDayToKeep",
            firstDayToKeep);

        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<int>
        DeleteOldOrphanSyncRunsAsync(
            int seasonId,
            int firstDayToKeep,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        DELETE FROM SyncRuns
        WHERE SeasonId = $seasonId
          AND DayId IN
          (
              SELECT DayId
              FROM Days
              WHERE SeasonId = $seasonId
                AND DayNumber < $firstDayToKeep
          )
          AND NOT EXISTS
          (
              SELECT 1
              FROM ClanChanges cc
              WHERE cc.SyncRunId =
                    SyncRuns.SyncRunId
          )
          AND NOT EXISTS
          (
              SELECT 1
              FROM MemberChanges mc
              WHERE mc.SyncRunId =
                    SyncRuns.SyncRunId
          )
          AND NOT EXISTS
          (
              SELECT 1
              FROM MemberMovements mm
              WHERE mm.SyncRunId =
                    SyncRuns.SyncRunId
          )
          AND NOT EXISTS
          (
              SELECT 1
              FROM AttackCaptureSessions acs
              WHERE acs.StartSyncRunId =
                    SyncRuns.SyncRunId
          );
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        command.Parameters.AddWithValue(
            "$firstDayToKeep",
            firstDayToKeep);

        return await command.ExecuteNonQueryAsync();
    }

    private async Task<int>
        FinalizeEndedSeasonsAsync(
            DateTime generatedAt,
            RetentionResult result,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        List<int> seasonIds = [];

        using (SqliteCommand command =
               connection.CreateCommand())
        {
            command.Transaction = transaction;

            command.CommandText =
            """
            SELECT SeasonId
            FROM Seasons
            WHERE EndTime <= $generatedAt
              AND IsCompleted = 0;
            """;

            command.Parameters.AddWithValue(
                "$generatedAt",
                generatedAt.ToString("O"));

            using SqliteDataReader reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                seasonIds.Add(
                    reader.GetInt32(0));
            }
        }

        foreach (int seasonId in seasonIds)
        {
            long? lastDayId =
                await GetLastDayIdAsync(
                    seasonId,
                    connection,
                    transaction);

            if (lastDayId is not null)
            {
                await CreateDailySummariesAsync(
                    lastDayId.Value,
                    connection,
                    transaction);

                await MarkDayCompletedAsync(
                    lastDayId.Value,
                    connection,
                    transaction);

                result.SummarizedDays++;
            }

            int maximumDayNumber =
                await GetMaximumDayNumberAsync(
                    seasonId,
                    connection,
                    transaction);

            int firstDayToKeep =
                Math.Max(
                    1,
                    maximumDayNumber -
                    FinalSeasonDaysToKeep + 1);

            result.DeletedMemberChanges +=
                await DeleteOldMemberChangesAsync(
                    seasonId,
                    firstDayToKeep,
                    connection,
                    transaction);

            result.DeletedClanChanges +=
                await DeleteOldClanChangesAsync(
                    seasonId,
                    firstDayToKeep,
                    connection,
                    transaction);

            result.DeletedSyncRuns +=
                await DeleteOldOrphanSyncRunsAsync(
                    seasonId,
                    firstDayToKeep,
                    connection,
                    transaction);

            await MarkSeasonCompletedAsync(
                seasonId,
                connection,
                transaction);
        }

        return seasonIds.Count;
    }

    private static async Task<long?>
        GetLastDayIdAsync(
            int seasonId,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        SELECT DayId
        FROM Days
        WHERE SeasonId = $seasonId
        ORDER BY DayNumber DESC
        LIMIT 1;
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        object? value =
            await command.ExecuteScalarAsync();

        return value is null ||
               value == DBNull.Value
            ? null
            : Convert.ToInt64(value);
    }

    private static async Task<int>
        GetMaximumDayNumberAsync(
            int seasonId,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        SELECT COALESCE(MAX(DayNumber), 0)
        FROM Days
        WHERE SeasonId = $seasonId;
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        return Convert.ToInt32(
            await command.ExecuteScalarAsync());
    }

    private static async Task
        MarkSeasonCompletedAsync(
            int seasonId,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        UPDATE Seasons
        SET IsCompleted = 1
        WHERE SeasonId = $seasonId;
        """;

        command.Parameters.AddWithValue(
            "$seasonId",
            seasonId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?>
        GetStateAsync(
            string key,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        SELECT Value
        FROM MaintenanceState
        WHERE Key = $key;
        """;

        command.Parameters.AddWithValue(
            "$key",
            key);

        return Convert.ToString(
            await command.ExecuteScalarAsync());
    }

    private static async Task SaveStateAsync(
        string key,
        string value,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO MaintenanceState
        (
            Key,
            Value,
            UpdatedAt
        )
        VALUES
        (
            $key,
            $value,
            $updatedAt
        )

        ON CONFLICT(Key) DO UPDATE SET
            Value = excluded.Value,
            UpdatedAt = excluded.UpdatedAt;
        """;

        command.Parameters.AddWithValue(
            "$key",
            key);

        command.Parameters.AddWithValue(
            "$value",
            value);

        command.Parameters.AddWithValue(
            "$updatedAt",
            DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }
}