namespace PoteHub.Domain.Entities;

public class MemberProfile
{
    public int MemberId { get; set; }

    public string MemberName { get; set; } =
        string.Empty;

    public int Level { get; set; }

    public int ClanId { get; set; }

    public string ClanName { get; set; } =
        string.Empty;

    public int SeasonId { get; set; }

    public string SeasonName { get; set; } =
        string.Empty;

    public int CurrentReputation { get; set; }

    public int GlobalRank { get; set; }

    public int RequiredReputation { get; set; }

    public int RemainingReputation { get; set; }

    public decimal ProgressPercentage { get; set; }

    public bool IsRewardEligible { get; set; }

    public DateTime? RewardQualifiedAt { get; set; }
    public int DayNumber { get; set; }

    public int WaveNumber { get; set; }

    public DateTime WaveStartTime { get; set; }

    public DateTime WaveEndTime { get; set; }

    public string WaveStatus { get; set; } =
        string.Empty;

    public int WaveReputationGain { get; set; }

    public int WaveReputationDeduction { get; set; }

    public int WaveNetReputation { get; set; }

    public DateTime LastUpdatedAt { get; set; }
}