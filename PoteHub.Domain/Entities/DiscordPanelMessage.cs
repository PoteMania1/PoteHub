namespace PoteHub.Domain.Entities;

public class DiscordPanelMessage
{
    public long PanelMessageId { get; set; }

    public long PanelId { get; set; }

    public int SeasonId { get; set; }

    public long DayId { get; set; }

    public long WaveId { get; set; }

    public string MessageId { get; set; } =
        string.Empty;

    public bool IsCurrent { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? FinalizedAt { get; set; }
}