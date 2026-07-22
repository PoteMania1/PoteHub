namespace PoteHub.Domain.Entities;

public class AttackCaptureSession
{
    public long SessionId { get; set; }

    public string GuildId { get; set; } =
        string.Empty;

    public string ChannelId { get; set; } =
        string.Empty;

    public string RequestedByDiscordId { get; set; } =
        string.Empty;

    public int ClanId { get; set; }

    public string ClanName { get; set; } =
        string.Empty;

    public int SeasonId { get; set; }

    public string SeasonName { get; set; } =
        string.Empty;

    public long StartWaveId { get; set; }

    public long StartSyncRunId { get; set; }

    public int WaveCount { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string Status { get; set; } =
        "Active";

    public int StartDayNumber { get; set; }

    public int StartWaveNumber { get; set; }
}