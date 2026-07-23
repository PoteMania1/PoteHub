using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Migrations;

public class Migration012DailySummariesAndRetention
    : IDatabaseMigration
{
    public int Version => 12;

    public string Name =>
        "Daily summaries and retention";

    public async Task ApplyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS
            ClanDailySummaries
        (
            DayId INTEGER NOT NULL,
            SeasonId INTEGER NOT NULL,
            ClanId INTEGER NOT NULL,

            ReputationStart INTEGER NOT NULL,
            ReputationEnd INTEGER NOT NULL,
            ReputationGain INTEGER NOT NULL,
            ReputationDeduction INTEGER NOT NULL,

            RankStart INTEGER NOT NULL,
            RankEnd INTEGER NOT NULL,

            CreatedAt TEXT NOT NULL,

            PRIMARY KEY (DayId, ClanId),

            FOREIGN KEY (DayId)
                REFERENCES Days(DayId),

            FOREIGN KEY (SeasonId)
                REFERENCES Seasons(SeasonId),

            FOREIGN KEY (ClanId)
                REFERENCES Clans(ClanId)
        );

        CREATE TABLE IF NOT EXISTS
            MemberDailySummaries
        (
            DayId INTEGER NOT NULL,
            SeasonId INTEGER NOT NULL,
            MemberId INTEGER NOT NULL,
            ClanId INTEGER NOT NULL,

            ReputationStart INTEGER NOT NULL,
            ReputationEnd INTEGER NOT NULL,
            ReputationGain INTEGER NOT NULL,
            ReputationDeduction INTEGER NOT NULL,

            ActiveChanges INTEGER NOT NULL,
            DailyRank INTEGER NULL,

            CreatedAt TEXT NOT NULL,

            PRIMARY KEY
            (
                DayId,
                MemberId,
                ClanId
            ),

            FOREIGN KEY (DayId)
                REFERENCES Days(DayId),

            FOREIGN KEY (SeasonId)
                REFERENCES Seasons(SeasonId),

            FOREIGN KEY (MemberId)
                REFERENCES Members(MemberId),

            FOREIGN KEY (ClanId)
                REFERENCES Clans(ClanId)
        );

        CREATE TABLE IF NOT EXISTS
            MaintenanceState
        (
            Key TEXT PRIMARY KEY,
            Value TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS
            IX_ClanDailySummaries_SeasonDay
        ON ClanDailySummaries
        (
            SeasonId,
            DayId
        );

        CREATE INDEX IF NOT EXISTS
            IX_MemberDailySummaries_SeasonDay
        ON MemberDailySummaries
        (
            SeasonId,
            DayId
        );

        CREATE INDEX IF NOT EXISTS
            IX_MemberDailySummaries_Ranking
        ON MemberDailySummaries
        (
            DayId,
            DailyRank
        );
        """;

        await command.ExecuteNonQueryAsync();
    }
}