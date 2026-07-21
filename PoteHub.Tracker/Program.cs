using PoteHub.Api.Clients;
using PoteHub.Database.Data;
using PoteHub.Tracker.Configuration;
using PoteHub.Tracker.Logging;
using PoteHub.Tracker.Models;
using PoteHub.Tracker.Presentation;
using PoteHub.Tracker.Services;
using System.Diagnostics;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

TrackerOptions options = new();

string dataDirectory = Path.Combine(
    Environment.GetFolderPath(
        Environment.SpecialFolder
            .LocalApplicationData),
    "PoteHub");

string databasePath = Path.Combine(
    dataDirectory,
    "potehub.db");

string logsDirectory = Path.Combine(
    dataDirectory,
    "logs");

DatabaseConnection database =
    new(databasePath);

FileLogger logger =
    new(logsDirectory);

using NinjaSagaApiClient apiClient = new(
    options.HttpTimeout,
    options.HttpMaxAttempts);

SyncService syncService =
    new(apiClient, database);

using CancellationTokenSource cancellation =
    new();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;

    Console.WriteLine();
    Console.WriteLine(
        "Deteniendo PoteHub...");

    cancellation.Cancel();
};

Console.WriteLine("PoteHub Tracker iniciado.");
Console.WriteLine(
    $"Base de datos: {databasePath}");

Console.WriteLine(
    $"Intervalo: " +
    $"{options.SyncInterval.TotalSeconds} segundos");

Console.WriteLine(
    "Presioná Ctrl + C para detenerlo.");

await logger.LogInformationAsync(
    "PoteHub Tracker iniciado.");

while (!cancellation.IsCancellationRequested)
{
    Stopwatch stopwatch =
        Stopwatch.StartNew();

    try
    {
        Console.WriteLine();
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] " +
            "Descargando información...");

        SyncResult result =
            await syncService.RunAsync(
                cancellation.Token);

        ConsoleSyncReporter.Print(result);

        string logMessage =
            result.AlreadyProcessed
                ? $"Respuesta ya procesada: " +
                  $"{result.GeneratedAt}"
                : $"Sincronización completada. " +
                  $"Clanes modificados: " +
                  $"{result.ChangedClans}. " +
                  $"Miembros modificados: " +
                  $"{result.ChangedParticipations}. " +
                  $"Entradas: {result.EnteredMembers}. " +
                  $"Cambios de clan: " +
                  $"{result.ChangedClanMembers}.";

        await logger.LogInformationAsync(
            logMessage);
    }
    catch (OperationCanceledException)
        when (cancellation.IsCancellationRequested)
    {
        break;
    }
    catch (Exception exception)
    {
        Console.ForegroundColor =
            ConsoleColor.Red;

        Console.WriteLine();
        Console.WriteLine(
            "La sincronización falló.");

        Console.WriteLine(exception.Message);

        Console.ResetColor();

        await logger.LogErrorAsync(
            exception);
    }

    stopwatch.Stop();

    TimeSpan remainingDelay =
        options.SyncInterval -
        stopwatch.Elapsed;

    if (remainingDelay <= TimeSpan.Zero)
    {
        continue;
    }

    try
    {
        await Task.Delay(
            remainingDelay,
            cancellation.Token);
    }
    catch (OperationCanceledException)
        when (cancellation.IsCancellationRequested)
    {
        break;
    }
}

await logger.LogInformationAsync(
    "PoteHub Tracker detenido.");

Console.WriteLine(
    "PoteHub se detuvo correctamente.");