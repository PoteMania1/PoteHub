using Discord;
using Discord.WebSocket;
using PoteHub.Database.RepositoryBase;
using PoteHub.Domain.Entities;
using System.Text;

namespace PoteHub.Discord.Services;

public class AttackCaptureService
{
    private static readonly TimeSpan CheckInterval =
        TimeSpan.FromSeconds(5);

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

            if (!ulong.TryParse(
                    session.ChannelId,
                    out ulong channelId))
            {
                continue;
            }

            if (client.GetChannel(channelId)
                is not IMessageChannel channel)
            {
                continue;
            }

            List<AttackCaptureEntry> entries =
                await _repository.GetEntriesAsync(
                    session);

            byte[] fileContents =
                BuildTextFile(session, entries);

            using MemoryStream stream =
                new(fileContents);

            string fileName =
                $"registro-clan-{session.ClanId}-" +
                $"waves-{session.WaveCount}.txt";

            await channel.SendFileAsync(
                stream,
                fileName,
                $"✅ Registro finalizado para " +
                $"**{session.ClanName}**.\n" +
                $"Waves registradas: " +
                $"**{session.WaveCount}**.");

            await _repository.MarkCompletedAsync(
                session.SessionId);
        }
    }

    private static byte[] BuildTextFile(
        AttackCaptureSession session,
        List<AttackCaptureEntry> entries)
    {
        StringBuilder text = new();

        text.AppendLine(
            "PoteHub - Registro de actividad");

        text.AppendLine(
            $"Clan: {session.ClanName} " +
            $"(ID {session.ClanId})");

        text.AppendLine(
            $"Temporada: {session.SeasonName}");

        text.AppendLine(
            $"Waves solicitadas: {session.WaveCount}");

        text.AppendLine(
            $"Inicio del registro: " +
            $"{session.StartedAt:yyyy-MM-dd HH:mm:ss} UTC");

        text.AppendLine();

        if (entries.Count == 0)
        {
            text.AppendLine(
                "No se detectaron incrementos de " +
                "reputación durante el registro.");

            return EncodeWithBom(text.ToString());
        }

        foreach (IGrouping<long, AttackCaptureEntry>
                 waveGroup in entries.GroupBy(
                     entry => entry.WaveId))
        {
            AttackCaptureEntry wave =
                waveGroup.First();

            text.AppendLine(
                "========================================");

            text.AppendLine(
                $"{session.SeasonName} - " +
                $"Día {wave.DayNumber} - " +
                $"Wave {wave.WaveNumber}");

            text.AppendLine(
                "========================================");

            text.AppendLine();

            foreach (IGrouping<int, AttackCaptureEntry>
                     memberGroup in waveGroup
                         .GroupBy(entry => entry.MemberId)
                         .OrderByDescending(
                             group => group.Sum(
                                 entry =>
                                     entry.ReputationAmount)))
            {
                string memberName =
                    memberGroup.First().MemberName;

                List<int> attacks =
                    memberGroup
                        .Select(
                            entry =>
                                entry.ReputationAmount)
                        .ToList();

                text.Append(
                    $"Ataques: {attacks.Count} | ");

                text.Append(
                    $"{memberName}: ");

                text.AppendLine(
                    string.Join(" ", attacks));
            }

            text.AppendLine();
        }

        return EncodeWithBom(text.ToString());
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