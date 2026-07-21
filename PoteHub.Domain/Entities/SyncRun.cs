namespace PoteHub.Domain.Entities;

public class SyncRun
{
    public long SyncRunId { get; set; }

    public int SeasonId { get; set; }

    public DateTime ExecutedAt { get; set; }

    public DateTime GeneratedAt { get; set; }

    public int ClanChanges { get; set; }

    public int MemberChanges { get; set; }

    public int EnteredMembers { get; set; }

    public int ChangedClanMembers { get; set; }

    public int MissingMembers { get; set; }
}