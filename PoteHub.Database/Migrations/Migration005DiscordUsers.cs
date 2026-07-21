using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Migrations;

public class Migration005DiscordUsers
    : IDatabaseMigration
{
    public int Version => 5;

    public string Name => "Add Discord users";

    public async Task ApplyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS DiscordUsers
        (
            DiscordId TEXT PRIMARY KEY,
            MemberId INTEGER NOT NULL,
            LinkedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,

            FOREIGN KEY (MemberId)
                REFERENCES Members(MemberId)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS
            UX_DiscordUsers_ActiveMember
        ON DiscordUsers(MemberId)
        WHERE IsActive = 1;

        CREATE INDEX IF NOT EXISTS
            IX_DiscordUsers_MemberId
        ON DiscordUsers(MemberId);
        """;

        await command.ExecuteNonQueryAsync();
    }
}