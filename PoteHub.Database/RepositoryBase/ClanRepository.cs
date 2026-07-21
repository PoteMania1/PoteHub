using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class ClanRepository : RepositoryBase
{
    public ClanRepository(DatabaseConnection database)
        : base(database)
    {
    }

    public async Task SaveAsync(
     Clan clan,
     SqliteConnection connection,
     SqliteTransaction transaction)
    {

        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO Clans
        (
            ClanId,
            Name,
            MasterName
        )
        VALUES
        (
            $id,
            $name,
            $master
        )
        ON CONFLICT(ClanId) DO UPDATE SET
            Name = excluded.Name,
            MasterName = excluded.MasterName;
        """;

        command.Parameters.AddWithValue("$id", clan.ClanId);
        command.Parameters.AddWithValue("$name", clan.Name);
        command.Parameters.AddWithValue("$master", clan.MasterName);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Clan?> GetByIdAsync(
    int clanId,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
    SELECT
        ClanId,
        Name,
        MasterName
    FROM Clans
    WHERE ClanId = $clanId;
    """;

        command.Parameters.AddWithValue(
            "$clanId",
            clanId);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new Clan
        {
            ClanId = reader.GetInt32(0),
            Name = reader.GetString(1),
            MasterName = reader.GetString(2)
        };
    }
}