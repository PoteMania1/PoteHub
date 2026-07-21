namespace PoteHub.Domain.Entities;

public class MemberParticipation
{
    public int SeasonId { get; set; }

    public int MemberId { get; set; }

    public int ClanId { get; set; }

    public int Reputation { get; set; }

    public bool IsActive { get; set; }

    public DateTime? LastSeenAt { get; set; }
}