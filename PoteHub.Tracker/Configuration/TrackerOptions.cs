namespace PoteHub.Tracker.Configuration;

public class TrackerOptions
{
    public TimeSpan SyncInterval { get; set; } =
        TimeSpan.FromMilliseconds(500);

    public int HttpMaxAttempts { get; set; } = 3;

    public TimeSpan HttpTimeout { get; set; } =
        TimeSpan.FromSeconds(15);
}