namespace PoteHub.Domain.Entities;

public class AttackCaptureEntry
{
    public long WaveId { get; set; }

    public int DayNumber { get; set; }

    public int WaveNumber { get; set; }

    public int MemberId { get; set; }

    public string MemberName { get; set; } =
        string.Empty;

    public int ReputationAmount { get; set; }

    public DateTime DetectedAt { get; set; }
}