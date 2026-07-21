using PoteHub.Tracker.Models;

namespace PoteHub.Tracker.Presentation;

public static class ConsoleSyncReporter
{
    public static void Print(SyncResult result)
    {
        if (result.AlreadyProcessed)
        {
            Console.WriteLine(
                $"La respuesta {result.GeneratedAt} " +
                "ya había sido procesada.");

            return;
        }

        foreach (string message in result.Messages)
        {
            Console.WriteLine(message);
        }

        Console.WriteLine();
        Console.WriteLine("Sincronización finalizada.");
        Console.WriteLine(
            $"Clanes nuevos: {result.NewClans}");

        Console.WriteLine(
            $"Clanes modificados: {result.ChangedClans}");

        Console.WriteLine(
            $"Participaciones nuevas: " +
            $"{result.NewParticipations}");

        Console.WriteLine(
            $"Participaciones modificadas: " +
            $"{result.ChangedParticipations}");

        Console.WriteLine(
            $"Entradas a clanes: {result.EnteredMembers}");

        Console.WriteLine(
            $"Cambios de clan: {result.ChangedClanMembers}");

        Console.WriteLine(
            $"Ausentes en la API: {result.MissingMembers}");
    }
}