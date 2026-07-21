namespace PoteHub.Domain.Entities;

public class MemberRankingPanelData
{
    public int SeasonId { get; set; }

    public string SeasonName { get; set; } =
        string.Empty;

    public long DayId { get; set; }

    public int DayNumber { get; set; }

    public long WaveId { get; set; }

    public int WaveNumber { get; set; }

    public DateTime WaveStartTime { get; set; }

    public DateTime WaveEndTime { get; set; }

    public string WaveStatus { get; set; } =
        string.Empty;

    public DateTime LastUpdatedAt { get; set; }

    public int ClanId { get; set; }

    public string ClanName { get; set; } =
        string.Empty;

    public List<MemberRankingPanelEntry> Members
    {
        get;
        set;
    } = [];
}