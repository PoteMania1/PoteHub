using Microsoft.Data.Sqlite;

namespace PoteHub.Database.Migrations;

public interface IDatabaseMigration
{
    int Version { get; }

    string Name { get; }

    Task ApplyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction);
}