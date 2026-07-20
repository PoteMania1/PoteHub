namespace PoteHub.Domain.Entities;

public class Clan
{
    public int ClanId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string MasterName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public bool IsActive { get; set; }
}