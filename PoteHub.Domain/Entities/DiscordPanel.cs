namespace PoteHub.Domain.Entities;

public class DiscordPanel
{
    public long PanelId { get; set; }

    public string GuildId { get; set; } =
        string.Empty;

    public string PanelType { get; set; } =
        string.Empty;

    public string ChannelId { get; set; } =
        string.Empty;

    public int? ClanId { get; set; }

    public int? ComparisonClanId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}