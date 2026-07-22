using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Migrations;

public class Migration010ComparisonMemberMessages
    : IDatabaseMigration
{
    public int Version => 10;

    public string Name =>
        "Add secondary panel message";

    public async Task ApplyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand checkCommand =
            connection.CreateCommand();

        checkCommand.Transaction = transaction;

        checkCommand.CommandText =
        """
        PRAGMA table_info(
            DiscordPanelMessages
        );
        """;

        bool exists = false;

        using (SqliteDataReader reader =
               await checkCommand
                   .ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                string columnName =
                    reader.GetString(1);

                if (columnName.Equals(
                    "SecondaryMessageId",
                    StringComparison
                        .OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
        {
            return;
        }

        using SqliteCommand alterCommand =
            connection.CreateCommand();

        alterCommand.Transaction = transaction;

        alterCommand.CommandText =
        """
        ALTER TABLE DiscordPanelMessages
        ADD COLUMN SecondaryMessageId TEXT NULL;
        """;

        await alterCommand.ExecuteNonQueryAsync();
    }
}