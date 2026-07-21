using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;

namespace PoteHub.Database.Initialization;

public class DatabaseInitializer
{
    private readonly DatabaseConnection _database;

    public DatabaseInitializer(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task InitializeAsync()
    {
        await ExecuteNonQueryAsync("""
        DROP TABLE IF EXISTS Test;

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

            FOREIGN KEY (SeasonId) REFERENCES Seasons(SeasonId),
            FOREIGN KEY (ClanId) REFERENCES Clans(ClanId)
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

            FOREIGN KEY (SeasonId) REFERENCES Seasons(SeasonId),
            FOREIGN KEY (MemberId) REFERENCES Members(MemberId),
            FOREIGN KEY (ClanId) REFERENCES Clans(ClanId)
        );

                CREATE TABLE IF NOT EXISTS SyncRuns
        (
            SyncRunId INTEGER PRIMARY KEY AUTOINCREMENT,
            SeasonId INTEGER NOT NULL,
            ExecutedAt TEXT NOT NULL,
            ClanChanges INTEGER NOT NULL DEFAULT 0,
            MemberChanges INTEGER NOT NULL DEFAULT 0,

            FOREIGN KEY (SeasonId) REFERENCES Seasons(SeasonId)
        );

        CREATE TABLE IF NOT EXISTS ClanChanges
        (
            ClanChangeId INTEGER PRIMARY KEY AUTOINCREMENT,
            SyncRunId INTEGER NOT NULL,
            SeasonId INTEGER NOT NULL,
            ClanId INTEGER NOT NULL,
            PreviousRank INTEGER NOT NULL,
            CurrentRank INTEGER NOT NULL,
            PreviousReputation INTEGER NOT NULL,
            CurrentReputation INTEGER NOT NULL,
            ReputationDifference INTEGER NOT NULL,
            DetectedAt TEXT NOT NULL,

            FOREIGN KEY (SyncRunId) REFERENCES SyncRuns(SyncRunId),
            FOREIGN KEY (SeasonId) REFERENCES Seasons(SeasonId),
            FOREIGN KEY (ClanId) REFERENCES Clans(ClanId)
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

            FOREIGN KEY (SyncRunId) REFERENCES SyncRuns(SyncRunId),
            FOREIGN KEY (SeasonId) REFERENCES Seasons(SeasonId),
            FOREIGN KEY (MemberId) REFERENCES Members(MemberId),
            FOREIGN KEY (ClanId) REFERENCES Clans(ClanId)
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

            FOREIGN KEY (SyncRunId) REFERENCES SyncRuns(SyncRunId),
            FOREIGN KEY (SeasonId) REFERENCES Seasons(SeasonId),
            FOREIGN KEY (MemberId) REFERENCES Members(MemberId),
            FOREIGN KEY (FromClanId) REFERENCES Clans(ClanId),
            FOREIGN KEY (ToClanId) REFERENCES Clans(ClanId)
        );

        CREATE INDEX IF NOT EXISTS IX_MemberMovements_MemberId
        ON MemberMovements(MemberId);

        CREATE INDEX IF NOT EXISTS IX_MemberMovements_SyncRunId
        ON MemberMovements(SyncRunId);

        CREATE INDEX IF NOT EXISTS IX_ClanChanges_ClanId
        ON ClanChanges(ClanId);

        CREATE INDEX IF NOT EXISTS IX_MemberChanges_MemberId
        ON MemberChanges(MemberId);
        CREATE INDEX IF NOT EXISTS IX_MemberChanges_ClanId
        ON MemberChanges(ClanId);
        """);

        await AddColumnIfMissingAsync(
        "MemberParticipations",
        "IsActive",
        "INTEGER NOT NULL DEFAULT 1");

        await AddColumnIfMissingAsync(
            "MemberParticipations",
            "LastSeenAt",
            "TEXT NULL");

        await AddColumnIfMissingAsync(
            "ClanChanges",
            "PreviousMemberCount",
            "INTEGER NOT NULL DEFAULT 0");

        await AddColumnIfMissingAsync(
            "ClanChanges",
            "CurrentMemberCount",
            "INTEGER NOT NULL DEFAULT 0");

        await AddColumnIfMissingAsync(
            "ClanChanges",
            "PreviousDeduction",
            "INTEGER NOT NULL DEFAULT 0");

        await AddColumnIfMissingAsync(
            "ClanChanges",
            "CurrentDeduction",
            "INTEGER NOT NULL DEFAULT 0");
    }

    private async Task ExecuteNonQueryAsync(string sql)
    {
        using SqliteConnection connection =
            _database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command = connection.CreateCommand();

        command.CommandText = sql;

        await command.ExecuteNonQueryAsync();
    }

    private async Task AddColumnIfMissingAsync(
    string tableName,
    string columnName,
    string columnDefinition)
    {
        using SqliteConnection connection = _database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand checkCommand = connection.CreateCommand();

        checkCommand.CommandText = $"PRAGMA table_info({tableName});";

        using SqliteDataReader reader =
            await checkCommand.ExecuteReaderAsync();

        bool columnExists = false;

        while (await reader.ReadAsync())
        {
            string existingColumnName = reader.GetString(1);

            if (existingColumnName.Equals(
                columnName,
                StringComparison.OrdinalIgnoreCase))
            {
                columnExists = true;
                break;
            }
        }

        await reader.CloseAsync();

        if (columnExists)
        {
            return;
        }

        using SqliteCommand alterCommand = connection.CreateCommand();

        alterCommand.CommandText =
            $"ALTER TABLE {tableName} " +
            $"ADD COLUMN {columnName} {columnDefinition};";

        await alterCommand.ExecuteNonQueryAsync();
    }
}