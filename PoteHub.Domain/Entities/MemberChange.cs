namespace PoteHub.Domain.Entities;

public class MemberChange
{
    public long MemberChangeId { get; set; }

    public long SyncRunId { get; set; }

    public int SeasonId { get; set; }

    public int MemberId { get; set; }

    public int ClanId { get; set; }

    public int PreviousReputation { get; set; }

    public int CurrentReputation { get; set; }

    public int ReputationDifference { get; set; }

    public DateTime DetectedAt { get; set; }
}