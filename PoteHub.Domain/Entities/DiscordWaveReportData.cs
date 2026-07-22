namespace PoteHub.Domain.Entities;

public class DiscordWaveReportData
{
    public int SeasonId { get; set; }

    public string SeasonName { get; set; } =
        string.Empty;

    public long DayId { get; set; }

    public int DayNumber { get; set; }

    public long WaveId { get; set; }

    public int WaveNumber { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string Status { get; set; } =
        string.Empty;

    public int ClanId { get; set; }

    public string ClanName { get; set; } =
        string.Empty;

    public int ReputationGain { get; set; }

    public int ReputationDeduction { get; set; }

    public int NetReputation { get; set; }

    public List<MemberRankingPanelEntry> Members
    {
        get;
        set;
    } = [];
}