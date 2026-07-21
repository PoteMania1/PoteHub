namespace PoteHub.Domain.Entities;

public class MemberMovement
{
    public long MemberMovementId { get; set; }

    public long SyncRunId { get; set; }

    public int SeasonId { get; set; }

    public int MemberId { get; set; }

    public int? FromClanId { get; set; }

    public int? ToClanId { get; set; }

    public string MovementType { get; set; } = string.Empty;

    public DateTime DetectedAt { get; set; }
}