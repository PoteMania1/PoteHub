using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Migrations;

public class Migration009DiscordSecurity
    : IDatabaseMigration
{
    public int Version => 9;

    public string Name =>
        "Add Discord security and administration";

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
            DiscordCharacterPanels
        (
            GuildId TEXT PRIMARY KEY,

            ChannelId TEXT NOT NULL,

            MessageId TEXT NOT NULL,

            IsActive INTEGER NOT NULL
                DEFAULT 1,

            CreatedAt TEXT NOT NULL,

            UpdatedAt TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS
            IX_DiscordCharacterPanels_IsActive
        ON DiscordCharacterPanels(IsActive);
        """;

        await command.ExecuteNonQueryAsync();
    }
}