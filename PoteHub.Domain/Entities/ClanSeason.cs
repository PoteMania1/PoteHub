namespace PoteHub.Domain.Entities;

public class ClanSeason
{
    public int SeasonId { get; set; }

    public int ClanId { get; set; }

    public int Rank { get; set; }

    public int MemberCount { get; set; }

    public int Reputation { get; set; }

    public int Deduction { get; set; }
}