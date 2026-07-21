namespace PoteHub.Domain.Entities;

public class ClanRankingPanelEntry
{
    public int Rank { get; set; }

    public int ClanId { get; set; }

    public string ClanName { get; set; } =
        string.Empty;

    public int TotalReputation { get; set; }

    public int TotalDeduction { get; set; }

    public int WaveReputation { get; set; }

    public int WaveDeduction { get; set; }
}