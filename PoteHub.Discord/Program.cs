using PoteHub.Discord.Services;
using System.Text;
using PoteHub.Database.Data;
using PoteHub.Database.Initialization;

Console.OutputEncoding = Encoding.UTF8;

string? token =
    Environment.GetEnvironmentVariable(
        "POTEHUB_DISCORD_TOKEN");

if (string.IsNullOrWhiteSpace(token))
{
    Console.ForegroundColor =
        ConsoleColor.Red;

    Console.WriteLine(
        "No se encontró el token de Discord.");

    Console.WriteLine(
        "Configurá la variable " +
        "POTEHUB_DISCORD_TOKEN y reiniciá " +
        "Visual Studio.");

    Console.ResetColor();

    return;
}

using CancellationTokenSource cancellation =
    new();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;

    Console.WriteLine();
    Console.WriteLine(
        "Desconectando PoteHub...");

    cancellation.Cancel();
};

string dataDirectory = Path.Combine(
    Environment.GetFolderPath(
        Environment.SpecialFolder
            .LocalApplicationData),
    "PoteHub");

string databasePath = Path.Combine(
    dataDirectory,
    "potehub.db");

DatabaseConnection database =
    new(databasePath);

DatabaseInitializer initializer =
    new(database);

await initializer.InitializeAsync();

Console.WriteLine(
    $"Base de datos: {databasePath}");

DiscordBotService botService =
    new(database);

try
{
    Console.WriteLine(
        "Conectando PoteHub con Discord...");

    await botService.StartAsync(
        token,
        cancellation.Token);
}
catch (Exception exception)
{
    Console.ForegroundColor =
        ConsoleColor.Red;

    Console.WriteLine(
        "No se pudo conectar con Discord.");

    Console.WriteLine(exception.Message);

    Console.ResetColor();
}

Console.WriteLine(
    "PoteHub Discord se detuvo.");