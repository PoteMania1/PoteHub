namespace PoteHub.Tracker.Models;

public class SyncResult
{
    public bool AlreadyProcessed { get; set; }

    public string GeneratedAt { get; set; } = string.Empty;

    public int NewClans { get; set; }

    public int ChangedClans { get; set; }

    public int NewParticipations { get; set; }

    public int ChangedParticipations { get; set; }

    public int EnteredMembers { get; set; }

    public int ChangedClanMembers { get; set; }

    public int MissingMembers { get; set; }

    public List<string> Messages { get; set; } = [];
}