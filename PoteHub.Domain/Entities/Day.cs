namespace PoteHub.Domain.Entities;

public class Day
{
    public long DayId { get; set; }

    public int SeasonId { get; set; }

    public int DayNumber { get; set; }

    public DateTime ServerDate { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public bool IsCompleted { get; set; }
}