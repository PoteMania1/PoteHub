using PoteHub.Api.Models;
using PoteHub.Domain.Entities;

namespace PoteHub.Api.Mappers;

public static class MemberMapper
{
    public static Member ToDomain(MemberResponse response)
    {
        return new Member
        {
            MemberId = response.Id,
            Name = response.Name,
            Level = response.Level
        };
    }
}