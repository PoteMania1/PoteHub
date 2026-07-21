using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Migrations;

public class Migration001InitialSchema : IDatabaseMigration
{
    public int Version => 1;

    public string Name => "Initial schema";

    public async Task ApplyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS Seasons
        (
            SeasonId INTEGER PRIMARY KEY,
            Name TEXT NOT NULL,
            StartTime TEXT NOT NULL,
            EndTime TEXT NOT NULL,
            EndTimeTimestamp INTEGER NOT NULL,
            IsCompleted INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS Clans
        (
            ClanId INTEGER PRIMARY KEY,
            Name TEXT NOT NULL,
            MasterName TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Members
        (
            MemberId INTEGER PRIMARY KEY,
            Name TEXT NOT NULL,
            Level INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS ClanSeasons
        (
            SeasonId INTEGER NOT NULL,
            ClanId INTEGER NOT NULL,
            Rank INTEGER NOT NULL,
            MemberCount INTEGER NOT NULL,
            Reputation INTEGER NOT NULL,
            Deduction INTEGER NOT NULL,

            PRIMARY KEY (SeasonId, ClanId),

            FOREIGN KEY (SeasonId)
                REFERENCES Seasons(SeasonId),

            FOREIGN KEY (ClanId)
                REFERENCES Clans(ClanId)
        );

        CREATE TABLE IF NOT EXISTS MemberParticipations
        (
            SeasonId INTEGER NOT NULL,
            MemberId INTEGER NOT NULL,
            ClanId INTEGER NOT NULL,
            Reputation INTEGER NOT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,
            LastSeenAt TEXT NULL,

            PRIMARY KEY (SeasonId, MemberId, ClanId),

            FOREIGN KEY (SeasonId)
                REFERENCES Seasons(SeasonId),

            FOREIGN KEY (MemberId)
                REFERENCES Members(MemberId),

            FOREIGN KEY (ClanId)
                REFERENCES Clans(ClanId)
        );

        CREATE TABLE IF NOT EXISTS SyncRuns
        (
            SyncRunId INTEGER PRIMARY KEY AUTOINCREMENT,
            SeasonId INTEGER NOT NULL,
            ExecutedAt TEXT NOT NULL,
            ClanChanges INTEGER NOT NULL DEFAULT 0,
            MemberChanges INTEGER NOT NULL DEFAULT 0,

            FOREIGN KEY (SeasonId)
                REFERENCES Seasons(SeasonId)
        );

        CREATE TABLE IF NOT EXISTS ClanChanges
        (
            ClanChangeId INTEGER PRIMARY KEY AUTOINCREMENT,
            SyncRunId INTEGER NOT NULL,
            SeasonId INTEGER NOT NULL,
            ClanId INTEGER NOT NULL,
            PreviousRank INTEGER NOT NULL,
            CurrentRank INTEGER NOT NULL,
            PreviousMemberCount INTEGER NOT NULL DEFAULT 0,
            CurrentMemberCount INTEGER NOT NULL DEFAULT 0,
            PreviousReputation INTEGER NOT NULL,
            CurrentReputation INTEGER NOT NULL,
            ReputationDifference INTEGER NOT NULL,
            PreviousDeduction INTEGER NOT NULL DEFAULT 0,
            CurrentDeduction INTEGER NOT NULL DEFAULT 0,
            DetectedAt TEXT NOT NULL,

            FOREIGN KEY (SyncRunId)
                REFERENCES SyncRuns(SyncRunId),

            FOREIGN KEY (SeasonId)
                REFERENCES Seasons(SeasonId),

            FOREIGN KEY (ClanId)
                REFERENCES Clans(ClanId)
        );

        CREATE TABLE IF NOT EXISTS MemberChanges
        (
            MemberChangeId INTEGER PRIMARY KEY AUTOINCREMENT,
            SyncRunId INTEGER NOT NULL,
            SeasonId INTEGER NOT NULL,
            MemberId INTEGER NOT NULL,
            ClanId INTEGER NOT NULL,
            PreviousReputation INTEGER NOT NULL,
            CurrentReputation INTEGER NOT NULL,
            ReputationDifference INTEGER NOT NULL,
            DetectedAt TEXT NOT NULL,

            FOREIGN KEY (SyncRunId)
                REFERENCES SyncRuns(SyncRunId),

            FOREIGN KEY (SeasonId)
                REFERENCES Seasons(SeasonId),

            FOREIGN KEY (MemberId)
                REFERENCES Members(MemberId),

            FOREIGN KEY (ClanId)
                REFERENCES Clans(ClanId)
        );

        CREATE TABLE IF NOT EXISTS MemberMovements
        (
            MemberMovementId INTEGER PRIMARY KEY AUTOINCREMENT,
            SyncRunId INTEGER NOT NULL,
            SeasonId INTEGER NOT NULL,
            MemberId INTEGER NOT NULL,
            FromClanId INTEGER NULL,
            ToClanId INTEGER NULL,
            MovementType TEXT NOT NULL,
            DetectedAt TEXT NOT NULL,

            FOREIGN KEY (SyncRunId)
                REFERENCES SyncRuns(SyncRunId),

            FOREIGN KEY (SeasonId)
                REFERENCES Seasons(SeasonId),

            FOREIGN KEY (MemberId)
                REFERENCES Members(MemberId),

            FOREIGN KEY (FromClanId)
                REFERENCES Clans(ClanId),

            FOREIGN KEY (ToClanId)
                REFERENCES Clans(ClanId)
        );

        CREATE INDEX IF NOT EXISTS IX_ClanChanges_ClanId
            ON ClanChanges(ClanId);

        CREATE INDEX IF NOT EXISTS IX_MemberChanges_MemberId
            ON MemberChanges(MemberId);

        CREATE INDEX IF NOT EXISTS IX_MemberChanges_ClanId
            ON MemberChanges(ClanId);

        CREATE INDEX IF NOT EXISTS IX_MemberMovements_MemberId
            ON MemberMovements(MemberId);

        CREATE INDEX IF NOT EXISTS IX_MemberMovements_SyncRunId
            ON MemberMovements(SyncRunId);
        """;

        await command.ExecuteNonQueryAsync();
    }
}