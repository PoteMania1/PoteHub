using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Domain.Entities;

namespace PoteHub.Database.RepositoryBase;

public class DiscordAdministrationRepository
    : RepositoryBase
{
    public DiscordAdministrationRepository(
        DatabaseConnection database)
        : base(database)
    {
    }

    public async Task<DiscordCharacterPanel?>
        GetCharacterPanelAsync(
            string guildId)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            GuildId,
            ChannelId,
            MessageId,
            IsActive,
            CreatedAt,
            UpdatedAt

        FROM DiscordCharacterPanels

        WHERE GuildId = $guildId

        LIMIT 1;
        """;

        command.Parameters.AddWithValue(
            "$guildId",
            guildId);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new DiscordCharacterPanel
        {
            GuildId = reader.GetString(0),
            ChannelId = reader.GetString(1),
            MessageId = reader.GetString(2),
            IsActive = reader.GetInt32(3) == 1,
            CreatedAt =
                DateTime.Parse(reader.GetString(4)),
            UpdatedAt =
                DateTime.Parse(reader.GetString(5))
        };
    }

    public async Task SaveCharacterPanelAsync(
        string guildId,
        string channelId,
        string messageId)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        INSERT INTO DiscordCharacterPanels
        (
            GuildId,
            ChannelId,
            MessageId,
            IsActive,
            CreatedAt,
            UpdatedAt
        )
        VALUES
        (
            $guildId,
            $channelId,
            $messageId,
            1,
            $createdAt,
            $updatedAt
        )
        ON CONFLICT(GuildId)
        DO UPDATE SET
            ChannelId = excluded.ChannelId,
            MessageId = excluded.MessageId,
            IsActive = 1,
            UpdatedAt = excluded.UpdatedAt;
        """;

        string timestamp =
            DateTime.UtcNow.ToString("O");

        command.Parameters.AddWithValue(
            "$guildId",
            guildId);

        command.Parameters.AddWithValue(
            "$channelId",
            channelId);

        command.Parameters.AddWithValue(
            "$messageId",
            messageId);

        command.Parameters.AddWithValue(
            "$createdAt",
            timestamp);

        command.Parameters.AddWithValue(
            "$updatedAt",
            timestamp);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool>
        DisableCharacterPanelAsync(
            string guildId)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        UPDATE DiscordCharacterPanels

        SET
            IsActive = 0,
            UpdatedAt = $updatedAt

        WHERE GuildId = $guildId
          AND IsActive = 1;
        """;

        command.Parameters.AddWithValue(
            "$guildId",
            guildId);

        command.Parameters.AddWithValue(
            "$updatedAt",
            DateTime.UtcNow.ToString("O"));

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool>
        DisableWaveReportsAsync(
            string guildId)
    {
        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        UPDATE DiscordWaveReportSettings

        SET
            IsActive = 0,
            UpdatedAt = $updatedAt

        WHERE GuildId = $guildId
          AND IsActive = 1;
        """;

        command.Parameters.AddWithValue(
            "$guildId",
            guildId);

        command.Parameters.AddWithValue(
            "$updatedAt",
            DateTime.UtcNow.ToString("O"));

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<DiscordGuildConfiguration>
        GetConfigurationAsync(
            string guildId)
    {
        DiscordGuildConfiguration result = new()
        {
            GuildId = guildId
        };

        using SqliteConnection connection =
            Database.CreateConnection();

        await connection.OpenAsync();

        await LoadGeneralSettingsAsync(
            result,
            connection);

        await LoadCharacterPanelAsync(
            result,
            connection);

        await LoadWaveReportsAsync(
            result,
            connection);

        return result;
    }

    private static async Task LoadGeneralSettingsAsync(
        DiscordGuildConfiguration result,
        SqliteConnection connection)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            dgs.ClanRankingChannelId,
            dgs.MemberRankingChannelId,
            dgs.HomeClanId,
            c.Name

        FROM DiscordGuildSettings dgs

        LEFT JOIN Clans c
            ON c.ClanId = dgs.HomeClanId

        WHERE dgs.GuildId = $guildId

        LIMIT 1;
        """;

        command.Parameters.AddWithValue(
            "$guildId",
            result.GuildId);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return;
        }

        result.ClanRankingChannelId =
            reader.IsDBNull(0)
                ? null
                : reader.GetString(0);

        result.MemberRankingChannelId =
            reader.IsDBNull(1)
                ? null
                : reader.GetString(1);

        result.HomeClanId =
            reader.IsDBNull(2)
                ? null
                : reader.GetInt32(2);

        result.HomeClanName =
            reader.IsDBNull(3)
                ? null
                : reader.GetString(3);
    }

    private static async Task LoadCharacterPanelAsync(
        DiscordGuildConfiguration result,
        SqliteConnection connection)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            ChannelId,
            IsActive

        FROM DiscordCharacterPanels

        WHERE GuildId = $guildId

        LIMIT 1;
        """;

        command.Parameters.AddWithValue(
            "$guildId",
            result.GuildId);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return;
        }

        result.CharacterPanelChannelId =
            reader.GetString(0);

        result.CharacterPanelActive =
            reader.GetInt32(1) == 1;
    }

    private static async Task LoadWaveReportsAsync(
        DiscordGuildConfiguration result,
        SqliteConnection connection)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            wrs.ChannelId,
            wrs.ClanId,
            c.Name,
            wrs.IsActive

        FROM DiscordWaveReportSettings wrs

        JOIN Clans c
            ON c.ClanId = wrs.ClanId

        WHERE wrs.GuildId = $guildId

        LIMIT 1;
        """;

        command.Parameters.AddWithValue(
            "$guildId",
            result.GuildId);

        using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return;
        }

        result.WaveReportChannelId =
            reader.GetString(0);

        result.WaveReportClanId =
            reader.GetInt32(1);

        result.WaveReportClanName =
            reader.GetString(2);

        result.WaveReportsActive =
            reader.GetInt32(3) == 1;
    }
}