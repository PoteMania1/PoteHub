using PoteHub.Api.Models;
using PoteHub.Domain.Entities;
using System.Globalization;

namespace PoteHub.Api.Mappers;

public static class SeasonMapper
{
    public static Season ToDomain(SeasonResponse response)
    {
        return new Season
        {
            SeasonId = response.Id,
            Name = response.Name,
            StartTime = DateTime.SpecifyKind(
             DateTime.ParseExact(
             response.StartTime,
             "yyyy-MM-dd HH:mm:ss",
             CultureInfo.InvariantCulture),
             DateTimeKind.Utc),

            EndTime = DateTime.SpecifyKind(
             DateTime.ParseExact(
             response.EndTime,
             "yyyy-MM-dd HH:mm:ss",
             CultureInfo.InvariantCulture),
             DateTimeKind.Utc),
            EndTimeTimestamp = response.EndTimeTs,
            IsCompleted = false
        };
    }
}