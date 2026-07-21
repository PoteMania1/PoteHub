using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;

namespace PoteHub.Database.RepositoryBase;

public abstract class RepositoryBase
{
    protected readonly DatabaseConnection Database;

    protected RepositoryBase(DatabaseConnection database)
    {
        Database = database;
    }

    protected SqliteConnection CreateConnection()
    {
        return Database.CreateConnection();
    }
}