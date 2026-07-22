namespace PoteHub.Domain.Entities;

public class DiscordCharacterPanel
{
    public string GuildId { get; set; } =
        string.Empty;

    public string ChannelId { get; set; } =
        string.Empty;

    public string MessageId { get; set; } =
        string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}