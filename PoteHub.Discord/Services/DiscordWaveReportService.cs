using Discord;
using Discord.WebSocket;
using PoteHub.Database.RepositoryBase;
using PoteHub.Domain.Entities;
using System.Text;

namespace PoteHub.Discord.Services;

public class DiscordWaveReportService
{
    private static readonly TimeSpan
        CheckInterval =
            TimeSpan.FromSeconds(5);

    private readonly DiscordWaveReportRepository
        _repository;

    public DiscordWaveReportService(
        DiscordWaveReportRepository repository)
    {
        _repository = repository;
    }

    public async Task RunAsync(
        DiscordSocketClient client,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            "Publicador de reportes iniciado.");

        while (!cancellationToken
            .IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(
                    client,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken
                    .IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "Error publicando reportes:");

                Console.WriteLine(exception);
            }

            try
            {
                await Task.Delay(
                    CheckInterval,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken
                    .IsCancellationRequested)
            {
                break;
            }
        }

        Console.WriteLine(
            "Publicador de reportes detenido.");
    }

    private async Task PublishPendingAsync(
        DiscordSocketClient client,
        CancellationToken cancellationToken)
    {
        List<DiscordWaveReportSetting> settings =
            await _repository
                .GetActiveSettingsAsync();

        foreach (DiscordWaveReportSetting setting
                 in settings)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (!ulong.TryParse(
                    setting.ChannelId,
                    out ulong channelId))
            {
                continue;
            }

            if (client.GetChannel(channelId)
                is not IMessageChannel channel)
            {
                continue;
            }

            List<long> pendingWaveIds =
                await _repository
                    .GetPendingWaveIdsAsync(
                        setting);

            foreach (long waveId
                     in pendingWaveIds)
            {
                DiscordWaveReportData? report =
                    await _repository.GetReportAsync(
                        waveId,
                        setting.ClanId);

                if (report is null)
                {
                    continue;
                }

                Embed embed =
                    BuildReportEmbed(report);

                IUserMessage message =
                    await channel.SendMessageAsync(
                        embed: embed);

                await _repository.MarkPublishedAsync(
                    setting.GuildId,
                    waveId,
                    message.Id.ToString());
            }
        }
    }

    private static Embed BuildReportEmbed(
        DiscordWaveReportData report)
    {
        StringBuilder members = new();

        if (report.Members.Count == 0)
        {
            members.Append(
                "No hubo reputación registrada.");
        }
        else
        {
            foreach (MemberRankingPanelEntry member
            in report.Members)
            {
                members.AppendLine(
                    $"**{member.Rank}. " +
                    $"{member.MemberName}**");

                members.AppendLine(
                    $"Ganada: " +
                    $"**+{member.WaveReputationGain:N0}** " +
                    $"• Deducida: " +
                    $"**-{member.WaveReputationDeduction:N0}** " +
                    $"• Resultado: " +
                    $"**{member.WaveNetReputation:+#;-#;0}**");

                members.AppendLine();
            }
        }

        string status =
            report.Status == "Complete"
                ? "Completa"
                : "Incompleta";

        return new EmbedBuilder()
            .WithTitle(
                $"📊 Reporte de wave — " +
                $"{report.ClanName}")

            .WithColor(
                report.NetReputation >= 0
                    ? Color.Green
                    : Color.Red)

            .AddField(
                "Periodo",
                $"Día {report.DayNumber} • " +
                $"Wave {report.WaveNumber}\n" +
                $"{report.StartTime:HH:mm} – " +
                $"{report.EndTime:HH:mm}\n" +
                $"Estado: **{status}**",
                inline: true)

            .AddField(
                "Resultado del clan",
                $"Ganada: " +
                $"**+{report.ReputationGain:N0}**\n" +
                $"Deducida: " +
                $"**-{report.ReputationDeduction:N0}**\n" +
                $"Resultado: " +
                $"**{report.NetReputation:+#;-#;0}**",
                inline: true)

            .AddField(
                "Top miembros",
                members.ToString(),
                inline: false)

            .WithFooter(
                $"{report.SeasonName} • " +
                $"Wave ID {report.WaveId}")

            .WithCurrentTimestamp()
            .Build();
    }
}