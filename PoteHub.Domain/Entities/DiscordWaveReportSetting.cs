namespace PoteHub.Domain.Entities;

public class DiscordWaveReportSetting
{
    public string GuildId { get; set; } =
        string.Empty;

    public string ChannelId { get; set; } =
        string.Empty;

    public int ClanId { get; set; }

    public string ClanName { get; set; } =
        string.Empty;

    public long StartWaveId { get; set; }

    public bool IsActive { get; set; }
}