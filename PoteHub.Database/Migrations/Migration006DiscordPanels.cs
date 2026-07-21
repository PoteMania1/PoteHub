using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Migrations;

public class Migration006DiscordPanels
    : IDatabaseMigration
{
    public int Version => 6;

    public string Name =>
        "Add Discord public panels";

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
            DiscordGuildSettings
        (
            GuildId TEXT PRIMARY KEY,

            ClanRankingChannelId TEXT NULL,

            MemberRankingChannelId TEXT NULL,

            HomeClanId INTEGER NULL,

            UpdatedAt TEXT NOT NULL,

            FOREIGN KEY (HomeClanId)
                REFERENCES Clans(ClanId)
        );

        CREATE TABLE IF NOT EXISTS
            DiscordPanels
        (
            PanelId INTEGER
                PRIMARY KEY AUTOINCREMENT,

            GuildId TEXT NOT NULL,

            PanelType TEXT NOT NULL,

            ChannelId TEXT NOT NULL,

            ClanId INTEGER NULL,

            IsActive INTEGER NOT NULL
                DEFAULT 1,

            CreatedAt TEXT NOT NULL,

            UpdatedAt TEXT NOT NULL,

            FOREIGN KEY (ClanId)
                REFERENCES Clans(ClanId),

            UNIQUE (GuildId, PanelType)
        );

        CREATE TABLE IF NOT EXISTS
            DiscordPanelMessages
        (
            PanelMessageId INTEGER
                PRIMARY KEY AUTOINCREMENT,

            PanelId INTEGER NOT NULL,

            SeasonId INTEGER NOT NULL,

            DayId INTEGER NOT NULL,

            WaveId INTEGER NOT NULL,

            MessageId TEXT NOT NULL,

            IsCurrent INTEGER NOT NULL
                DEFAULT 1,

            CreatedAt TEXT NOT NULL,

            FinalizedAt TEXT NULL,

            FOREIGN KEY (PanelId)
                REFERENCES DiscordPanels(PanelId),

            FOREIGN KEY (SeasonId)
                REFERENCES Seasons(SeasonId),

            FOREIGN KEY (DayId)
                REFERENCES Days(DayId),

            FOREIGN KEY (WaveId)
                REFERENCES Waves(WaveId),

            UNIQUE (PanelId, WaveId)
        );

        CREATE INDEX IF NOT EXISTS
            IX_DiscordPanels_GuildId
        ON DiscordPanels(GuildId);

        CREATE INDEX IF NOT EXISTS
            IX_DiscordPanels_IsActive
        ON DiscordPanels(IsActive);

        CREATE INDEX IF NOT EXISTS
            IX_DiscordPanelMessages_PanelId
        ON DiscordPanelMessages(PanelId);

        CREATE INDEX IF NOT EXISTS
            IX_DiscordPanelMessages_WaveId
        ON DiscordPanelMessages(WaveId);

        CREATE UNIQUE INDEX IF NOT EXISTS
            UX_DiscordPanelMessages_Current
        ON DiscordPanelMessages(PanelId)
        WHERE IsCurrent = 1;
        """;

        await command.ExecuteNonQueryAsync();
    }
}