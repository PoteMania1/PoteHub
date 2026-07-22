using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Migrations;

public class Migration007ClanComparisons
    : IDatabaseMigration
{
    public int Version => 7;

    public string Name =>
        "Add persistent clan comparisons";

    public async Task ApplyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        ALTER TABLE DiscordPanels
        ADD COLUMN ComparisonClanId
            INTEGER NULL
            REFERENCES Clans(ClanId);

        CREATE INDEX IF NOT EXISTS
            IX_DiscordPanels_ComparisonClanId
        ON DiscordPanels(ComparisonClanId);
        """;

        await command.ExecuteNonQueryAsync();
    }
}