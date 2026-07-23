using Discord;
using Discord.WebSocket;
using PoteHub.Database.RepositoryBase;
using PoteHub.Domain.Entities;
using System.Text;

namespace PoteHub.Discord.Services;

public class AttackCaptureService
{
    private static readonly TimeSpan CheckInterval =
        TimeSpan.FromSeconds(2);

    private readonly AttackCaptureRepository
        _repository;

    public AttackCaptureService(
        AttackCaptureRepository repository)
    {
        _repository = repository;
    }

    public async Task RunAsync(
        DiscordSocketClient client,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            "Registrador de ataques iniciado.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CompleteFinishedSessionsAsync(
                    client,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "Error procesando registros:");

                Console.WriteLine(exception);
            }

            try
            {
                await Task.Delay(
                    CheckInterval,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        Console.WriteLine(
            "Registrador de ataques detenido.");
    }

    public async Task<bool> FinishNowAsync(
    DiscordSocketClient client,
    string guildId,
    string channelId)
    {
        List<AttackCaptureSession> sessions =
            await _repository.GetActiveAsync();

        AttackCaptureSession? session =
            sessions.FirstOrDefault(
                current =>
                    current.GuildId == guildId &&
                    current.ChannelId == channelId);

        if (session is null)
        {
            return false;
        }

        await PublishSessionAsync(
            client,
            session,
            wasStoppedManually: true);

        return true;
    }

    private async Task CompleteFinishedSessionsAsync(
        DiscordSocketClient client,
        CancellationToken cancellationToken)
    {
        List<AttackCaptureSession> sessions =
            await _repository.GetActiveAsync();

        foreach (AttackCaptureSession session
                 in sessions)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            bool finished =
                await _repository.IsFinishedAsync(
                    session);

            if (!finished)
            {
                continue;
            }

            await PublishSessionAsync(
                client,
                session,
                wasStoppedManually: false);
        }
    }

    private async Task PublishSessionAsync(
    DiscordSocketClient client,
    AttackCaptureSession session,
    bool wasStoppedManually)
    {
        if (!ulong.TryParse(
                session.ChannelId,
                out ulong channelId))
        {
            throw new InvalidOperationException(
                "El ID del canal del registro no es válido.");
        }

        if (client.GetChannel(channelId)
            is not IMessageChannel channel)
        {
            throw new InvalidOperationException(
                "No se encontró el canal del registro.");
        }

        List<AttackCaptureEntry> entries =
        await _repository.GetEntriesAsync(
        session);

        Console.WriteLine(
            $"Registro {session.SessionId}: " +
            $"{entries.Count} cambios positivos encontrados " +
            $"para {session.ClanName} desde " +
            $"{session.StartedAt:HH:mm:ss} UTC.");

                byte[] fileContents =
            BuildCsvFile(session, entries);

        using MemoryStream stream =
            new(fileContents);

        string fileName =
            $"registro-clan-{session.ClanId}-" +
            $"waves-{session.WaveCount}.csv";

        string message =
            wasStoppedManually
                ? "⏹️ Registro detenido manualmente para " +
                  $"**{session.ClanName}**.\n" +
                  "El archivo contiene toda la actividad " +
                  "recopilada hasta este momento."
                : "✅ Registro finalizado para " +
                  $"**{session.ClanName}**.\n" +
                  $"Waves registradas: " +
                  $"**{session.WaveCount}**.";

        await channel.SendFileAsync(
            stream,
            fileName,
            message);

        await _repository.MarkCompletedAsync(
            session.SessionId);
    }
    private static byte[] BuildCsvFile(
    AttackCaptureSession session,
    List<AttackCaptureEntry> entries)
    {
        const int intervalMilliseconds = 500;
        const int waveDurationSeconds = 1800;

        int totalIntervals =
            waveDurationSeconds * 1000 /
            intervalMilliseconds;

        StringBuilder csv = new();

        csv.Append(
            "Temporada;Dia;Wave;MiembroId;" +
            "Miembro;NumeroAtaques");

        for (int interval = 1;
             interval <= totalIntervals;
             interval++)
        {
            decimal elapsedSeconds =
                interval *
                intervalMilliseconds /
                1000m;

            csv.Append(';');

            csv.Append(
                elapsedSeconds.ToString(
                    "0.0",
                    System.Globalization
                        .CultureInfo.InvariantCulture));

            csv.Append(" segundos");
        }

        csv.AppendLine(";ReputacionTotal");

        IEnumerable<IGrouping<
            (long WaveId, int MemberId),
            AttackCaptureEntry>> groups =
            entries
                .GroupBy(
                    entry => (
                        entry.WaveId,
                        entry.MemberId))
                .OrderBy(
                    group =>
                        group.First().WaveId)
                .ThenBy(
                    group =>
                        group.First().MemberName);

        foreach (IGrouping<
                 (long WaveId, int MemberId),
                 AttackCaptureEntry> group
                 in groups)
        {
            List<AttackCaptureEntry> attacks =
                group
                    .OrderBy(
                        entry => entry.DetectedAt)
                    .ToList();

            AttackCaptureEntry first =
                attacks.First();

            int[] reputationByInterval =
                new int[totalIntervals];

            foreach (AttackCaptureEntry attack
                     in attacks)
            {
                double elapsedMilliseconds =
                    (attack.DetectedAt -
                     attack.WaveStartTime)
                    .TotalMilliseconds;

                int intervalIndex =
                    (int)Math.Ceiling(
                        elapsedMilliseconds /
                        intervalMilliseconds) - 1;

                intervalIndex =
                    Math.Clamp(
                        intervalIndex,
                        0,
                        totalIntervals - 1);

                reputationByInterval[intervalIndex] +=
                    attack.ReputationAmount;
            }

            int totalReputation =
                reputationByInterval.Sum();

            int attackCount =
                reputationByInterval.Count(
                    reputation => reputation > 0);

            csv.Append(
                EscapeCsv(session.SeasonName));

            csv.Append(';');
            csv.Append(first.DayNumber);
            csv.Append(';');
            csv.Append(first.WaveNumber);
            csv.Append(';');
            csv.Append(first.MemberId);
            csv.Append(';');

            csv.Append(
                EscapeCsv(first.MemberName));

            csv.Append(';');
            csv.Append(attackCount);

            foreach (int reputation
                     in reputationByInterval)
            {
                csv.Append(';');
                csv.Append(reputation);
            }

            csv.Append(';');
            csv.Append(totalReputation);
            csv.AppendLine();
        }

        return EncodeWithBom(csv.ToString());
    }

    private static string EscapeCsv(
        string value)
    {
        string escaped =
            value.Replace("\"", "\"\"");

        return $"\"{escaped}\"";
    }

    private static byte[] EncodeWithBom(
        string content)
    {
        UTF8Encoding encoding =
            new(encoderShouldEmitUTF8Identifier: true);

        byte[] preamble = encoding.GetPreamble();
        byte[] body = encoding.GetBytes(content);

        byte[] result =
            new byte[preamble.Length + body.Length];

        Buffer.BlockCopy(
            preamble,
            0,
            result,
            0,
            preamble.Length);

        Buffer.BlockCopy(
            body,
            0,
            result,
            preamble.Length,
            body.Length);

        return result;
    }
}