namespace PoteHub.Domain.Entities;

public class MemberRewardProgress
{
    public int MemberId { get; set; }

    public string MemberName { get; set; } =
        string.Empty;

    public int ClanId { get; set; }

    public string ClanName { get; set; } =
        string.Empty;

    public int CurrentReputation { get; set; }

    public int RequiredReputation { get; set; }

    public int RemainingReputation { get; set; }

    public decimal ProgressPercentage { get; set; }

    public bool IsEligible { get; set; }

    public DateTime? QualifiedAt { get; set; }
}