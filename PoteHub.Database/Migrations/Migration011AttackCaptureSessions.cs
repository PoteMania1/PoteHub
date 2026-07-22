using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Migrations;

public class Migration011AttackCaptureSessions
    : IDatabaseMigration
{
    public int Version => 11;

    public string Name =>
        "Add attack capture sessions";

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
            AttackCaptureSessions
        (
            SessionId INTEGER
                PRIMARY KEY AUTOINCREMENT,

            GuildId TEXT NOT NULL,
            ChannelId TEXT NOT NULL,
            RequestedByDiscordId TEXT NOT NULL,

            ClanId INTEGER NOT NULL,
            SeasonId INTEGER NOT NULL,

            StartWaveId INTEGER NOT NULL,
            StartSyncRunId INTEGER NOT NULL,
            WaveCount INTEGER NOT NULL,

            StartedAt TEXT NOT NULL,
            CompletedAt TEXT NULL,
            Status TEXT NOT NULL DEFAULT 'Active',

            FOREIGN KEY (ClanId)
                REFERENCES Clans(ClanId),

            FOREIGN KEY (SeasonId)
                REFERENCES Seasons(SeasonId),

            FOREIGN KEY (StartWaveId)
                REFERENCES Waves(WaveId),

            FOREIGN KEY (StartSyncRunId)
                REFERENCES SyncRuns(SyncRunId)
        );

        CREATE INDEX IF NOT EXISTS
            IX_AttackCaptureSessions_Status
        ON AttackCaptureSessions(Status);

        CREATE UNIQUE INDEX IF NOT EXISTS
            UX_AttackCaptureSessions_ActiveChannel
        ON AttackCaptureSessions(
            GuildId,
            ChannelId
        )
        WHERE Status = 'Active';
        """;

        await command.ExecuteNonQueryAsync();
    }
}