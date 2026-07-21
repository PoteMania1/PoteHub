using PoteHub.Api.Models;
using PoteHub.Domain.Entities;

namespace PoteHub.Api.Mappers;

public static class SeasonMapper
{
    public static Season ToDomain(SeasonResponse response)
    {
        return new Season
        {
            SeasonId = response.Id,
            Name = response.Name,
            StartTime = DateTime.Parse(response.StartTime),
            EndTime = DateTime.Parse(response.EndTime),
            EndTimeTimestamp = response.EndTimeTs,
            IsCompleted = false
        };
    }
}