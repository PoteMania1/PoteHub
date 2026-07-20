namespace PoteHub.Domain.Entities;

public class Season
{
    public int SeasonId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public long EndTimeTimestamp { get; set; }

    public bool IsCompleted { get; set; }
}