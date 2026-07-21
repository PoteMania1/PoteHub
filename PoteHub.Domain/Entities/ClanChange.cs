namespace PoteHub.Domain.Entities;

public class ClanChange
{
    public long ClanChangeId { get; set; }

    public long SyncRunId { get; set; }

    public int SeasonId { get; set; }

    public int ClanId { get; set; }

    public int PreviousRank { get; set; }

    public int CurrentRank { get; set; }

    public int PreviousMemberCount { get; set; }

    public int CurrentMemberCount { get; set; }

    public int PreviousReputation { get; set; }

    public int CurrentReputation { get; set; }

    public int ReputationDifference { get; set; }

    public int PreviousDeduction { get; set; }

    public int CurrentDeduction { get; set; }

    public DateTime DetectedAt { get; set; }
}