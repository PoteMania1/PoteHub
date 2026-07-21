using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class MemberRepository : RepositoryBase
{
    public MemberRepository(DatabaseConnection database)
        : base(database)
    {
    }

    public async Task SaveAsync(
    Member member,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
        """
        INSERT INTO Members
        (
            MemberId,
            Name,
            Level
        )
        VALUES
        (
            $id,
            $name,
            $level
        )
        ON CONFLICT(MemberId) DO UPDATE SET
            Name = excluded.Name,
            Level = excluded.Level;
        """;

        command.Parameters.AddWithValue("$id", member.MemberId);
        command.Parameters.AddWithValue("$name", member.Name);
        command.Parameters.AddWithValue("$level", member.Level);

        await command.ExecuteNonQueryAsync();
    }
}