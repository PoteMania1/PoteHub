namespace PoteHub.Domain.Entities;

public class MemberRankingEntry
{
    public int Rank { get; set; }

    public int MemberId { get; set; }

    public string MemberName { get; set; } =
        string.Empty;

    public int ClanId { get; set; }

    public string ClanName { get; set; } =
        string.Empty;

    public int CurrentReputation { get; set; }

    public int ReputationGain { get; set; }

    public int ReputationDeduction { get; set; }

    public int NetReputation { get; set; }
}