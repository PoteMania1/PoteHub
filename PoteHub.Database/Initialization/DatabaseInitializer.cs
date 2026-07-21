using PoteHub.Database.Data;
using PoteHub.Database.Migrations;

namespace PoteHub.Database.Initialization;

public class DatabaseInitializer
{
    private readonly DatabaseMigrator _migrator;

    public DatabaseInitializer(DatabaseConnection database)
    {
        _migrator = new DatabaseMigrator(database);
    }

    public async Task InitializeAsync()
    {
        await _migrator.MigrateAsync();
    }
}