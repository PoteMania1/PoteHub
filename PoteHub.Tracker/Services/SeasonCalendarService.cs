using Microsoft.Data.Sqlite;
using PoteHub.Database.RepositoryBase;
using PoteHub.Domain.Entities;

namespace PoteHub.Tracker.Services;

public class SeasonCalendarService
{
    private readonly CalendarRepository _repository;

    public SeasonCalendarService(
        CalendarRepository repository)
    {
        _repository = repository;
    }

    public async Task EnsureCalendarAsync(
        Season season,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        DateTime seasonStart =
            season.StartTime.Date;

        DateTime seasonEnd =
            season.EndTime.Date;

        int totalDays =
            (seasonEnd - seasonStart).Days;

        for (int dayIndex = 0;
             dayIndex < totalDays;
             dayIndex++)
        {
            DateTime dayStart =
                seasonStart.AddDays(dayIndex);

            DateTime dayEnd =
                dayStart.AddDays(1);

            Day day = new()
            {
                SeasonId = season.SeasonId,
                DayNumber = dayIndex + 1,
                ServerDate = dayStart.Date,
                StartTime = dayStart,
                EndTime = dayEnd,
                IsCompleted = false
            };

            await _repository.SaveDayAsync(
                day,
                connection,
                transaction);

            Day? storedDay =
                await _repository.GetDayAsync(
                    season.SeasonId,
                    day.DayNumber,
                    connection,
                    transaction);

            if (storedDay is null)
            {
                throw new InvalidOperationException(
                    $"No se pudo crear el día " +
                    $"{day.DayNumber}.");
            }

            for (int waveIndex = 0;
                 waveIndex < 48;
                 waveIndex++)
            {
                DateTime waveStart =
                    dayStart.AddMinutes(
                        waveIndex * 30);

                Wave wave = new()
                {
                    DayId = storedDay.DayId,
                    SeasonId = season.SeasonId,
                    WaveNumber = waveIndex + 1,
                    StartTime = waveStart,
                    EndTime = waveStart.AddMinutes(30),
                    Status = "Pending"
                };

                await _repository.SaveWaveAsync(
                    wave,
                    connection,
                    transaction);
            }
        }
    }

    public async Task<(Day Day, Wave Wave)>
        GetCurrentWaveAsync(
            Season season,
            DateTime generatedAt,
            SqliteConnection connection,
            SqliteTransaction transaction)
    {
        if (generatedAt < season.StartTime ||
            generatedAt >= season.EndTime)
        {
            throw new InvalidOperationException(
                "La respuesta de la API está fuera " +
                "del período de la temporada.");
        }

        int dayNumber =
            (generatedAt.Date -
             season.StartTime.Date).Days + 1;

        int waveNumber =
            generatedAt.Hour * 2 +
            (generatedAt.Minute >= 30 ? 2 : 1);

        Day? day = await _repository.GetDayAsync(
            season.SeasonId,
            dayNumber,
            connection,
            transaction);

        if (day is null)
        {
            throw new InvalidOperationException(
                $"No se encontró el día {dayNumber}.");
        }

        Wave? wave = await _repository.GetWaveAsync(
            day.DayId,
            waveNumber,
            connection,
            transaction);

        if (wave is null)
        {
            throw new InvalidOperationException(
                $"No se encontró la wave " +
                $"{waveNumber} del día {dayNumber}.");
        }

        return (day, wave);
    }
}