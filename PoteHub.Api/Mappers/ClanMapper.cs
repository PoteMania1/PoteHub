using PoteHub.Api.Models;
using PoteHub.Domain.Entities;

namespace PoteHub.Api.Mappers;

public static class ClanMapper
{
    public static Clan ToDomain(ClanResponse response)
    {
        return new Clan
        {
            ClanId = response.Id,
            Name = response.Name,
            MasterName = response.Master
        };
    }
}