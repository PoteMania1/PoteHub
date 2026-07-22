namespace PoteHub.Domain.Entities;

public class DiscordGuildConfiguration
{
    public string GuildId { get; set; } =
        string.Empty;

    public string? ClanRankingChannelId
    {
        get;
        set;
    }

    public string? MemberRankingChannelId
    {
        get;
        set;
    }

    public int? HomeClanId { get; set; }

    public string? HomeClanName { get; set; }

    public string? CharacterPanelChannelId
    {
        get;
        set;
    }

    public bool CharacterPanelActive
    {
        get;
        set;
    }

    public string? WaveReportChannelId
    {
        get;
        set;
    }

    public int? WaveReportClanId
    {
        get;
        set;
    }

    public string? WaveReportClanName
    {
        get;
        set;
    }

    public bool WaveReportsActive
    {
        get;
        set;
    }
}