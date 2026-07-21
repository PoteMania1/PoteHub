using PoteHub.Domain.Enums;

namespace PoteHub.Domain.Entities;

public class DiscordLinkResult
{
    public DiscordLinkStatus Status { get; set; }

    public int MemberId { get; set; }

    public string MemberName { get; set; } =
        string.Empty;

    public string ClanName { get; set; } =
        string.Empty;
}