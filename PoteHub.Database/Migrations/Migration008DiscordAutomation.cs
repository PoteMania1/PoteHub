using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Migrations;

public class Migration008DiscordAutomation
    : IDatabaseMigration
{
    public int Version => 8;

    public string Name =>
        "Add Discord wave report automation";

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
            DiscordWaveReportSettings
        (
            GuildId TEXT PRIMARY KEY,

            ChannelId TEXT NOT NULL,

            ClanId INTEGER NOT NULL,

            StartWaveId INTEGER NOT NULL,

            IsActive INTEGER NOT NULL
                DEFAULT 1,

            CreatedAt TEXT NOT NULL,

            UpdatedAt TEXT NOT NULL,

            FOREIGN KEY (ClanId)
                REFERENCES Clans(ClanId),

            FOREIGN KEY (StartWaveId)
                REFERENCES Waves(WaveId)
        );

        CREATE TABLE IF NOT EXISTS
            DiscordWaveReports
        (
            GuildId TEXT NOT NULL,

            WaveId INTEGER NOT NULL,

            MessageId TEXT NOT NULL,

            PublishedAt TEXT NOT NULL,

            PRIMARY KEY
            (
                GuildId,
                WaveId
            ),

            FOREIGN KEY (WaveId)
                REFERENCES Waves(WaveId)
        );

        CREATE INDEX IF NOT EXISTS
            IX_DiscordWaveReportSettings_IsActive
        ON DiscordWaveReportSettings(IsActive);

        CREATE INDEX IF NOT EXISTS
            IX_DiscordWaveReports_WaveId
        ON DiscordWaveReports(WaveId);
        """;

        await command.ExecuteNonQueryAsync();
    }
}