namespace PoteHub.Domain.Entities;

public class Member
{
    public int MemberId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Level { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public bool IsActive { get; set; }
}