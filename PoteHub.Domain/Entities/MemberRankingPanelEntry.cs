namespace PoteHub.Domain.Entities;

public class MemberRankingPanelEntry
{
    public int Rank { get; set; }

    public int MemberId { get; set; }

    public string MemberName { get; set; } =
        string.Empty;

    public int Level { get; set; }

    public int TotalReputation { get; set; }

    public int WaveReputationGain { get; set; }

    public int WaveReputationDeduction { get; set; }

    public int WaveNetReputation { get; set; }
}