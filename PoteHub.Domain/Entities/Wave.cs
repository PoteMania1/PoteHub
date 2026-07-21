namespace PoteHub.Domain.Entities;


public class Wave
{
    public long WaveId { get; set; }

    public long DayId { get; set; }

    public int SeasonId { get; set; }

    public int WaveNumber { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string Status { get; set; } = "Pending";

    public int SuccessfulSyncCount { get; set; }

    public DateTime? FirstGeneratedAt { get; set; }

    public DateTime? LastGeneratedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}