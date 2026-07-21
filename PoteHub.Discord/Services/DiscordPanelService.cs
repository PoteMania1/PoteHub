using System.Text;
using Discord;
using Discord.WebSocket;
using PoteHub.Database.RepositoryBase;
using PoteHub.Domain.Entities;

namespace PoteHub.Discord.Services;

public class DiscordPanelService
{
    public const string ClanRanking =
        "ClanRanking";

    public const string HomeClanMembers =
        "HomeClanMembers";

    private static readonly TimeSpan
        UpdateInterval =
            TimeSpan.FromSeconds(30);

    private readonly DiscordPanelRepository
        _repository;

    public DiscordPanelService(
        DiscordPanelRepository repository)
    {
        _repository = repository;
    }

    public async Task ConfigureClanRankingAsync(
        ulong guildId,
        ulong channelId)
    {
        await _repository.ConfigureAsync(
            guildId.ToString(),
            ClanRanking,
            channelId.ToString(),
            clanId: null);
    }

    public async Task<string>
        ConfigureHomeClanAsync(
            ulong guildId,
            ulong channelId,
            int clanId)
    {
        string? clanName =
            await _repository.GetClanNameAsync(
                clanId);

        if (clanName is null)
        {
            throw new ArgumentException(
                $"No existe un clan con ID " +
                $"{clanId}.");
        }

        await _repository.ConfigureAsync(
            guildId.ToString(),
            HomeClanMembers,
            channelId.ToString(),
            clanId);

        return clanName;
    }

    public async Task RunUpdaterAsync(
        DiscordSocketClient client,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            "Actualizador de paneles iniciado.");

        while (!cancellationToken
            .IsCancellationRequested)
        {
            try
            {
                await UpdatePanelsAsync(
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
                Console.ForegroundColor =
                    ConsoleColor.Red;

                Console.WriteLine(
                    "Error actualizando paneles:");

                Console.WriteLine(exception);

                Console.ResetColor();
            }

            try
            {
                await Task.Delay(
                    UpdateInterval,
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
            "Actualizador de paneles detenido.");
    }

    private async Task UpdatePanelsAsync(
        DiscordSocketClient client,
        CancellationToken cancellationToken)
    {
        List<DiscordPanel> panels =
            await _repository.GetActiveAsync();

        foreach (DiscordPanel panel in panels)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (panel.PanelType != ClanRanking)
            {
                continue;
            }

            await UpdateClanRankingAsync(
                client,
                panel);
        }
    }

    private async Task UpdateClanRankingAsync(
    DiscordSocketClient client,
    DiscordPanel panel)
    {
        ClanRankingPanelData? data =
            await _repository
                .GetCurrentClanRankingAsync(
                    limit: 10);

        if (data is null ||
            data.Clans.Count == 0)
        {
            return;
        }

        if (!ulong.TryParse(
            panel.ChannelId,
            out ulong channelId))
        {
            Console.WriteLine(
                $"Canal inválido para el panel " +
                $"{panel.PanelId}.");

            return;
        }

        SocketTextChannel? channel =
            client.GetChannel(channelId)
                as SocketTextChannel;

        if (channel is null)
        {
            Console.WriteLine(
                $"No se encontró el canal " +
                $"{panel.ChannelId}.");

            return;
        }

        Embed embed =
            BuildClanRankingEmbed(data);

        DiscordPanelMessage? currentMessage =
            await _repository
                .GetCurrentMessageAsync(
                    panel.PanelId);

        if (currentMessage is not null &&
            currentMessage.WaveId ==
                data.WaveId)
        {
            bool updated =
                await TryUpdateMessageAsync(
                    channel,
                    currentMessage.MessageId,
                    embed);

            if (updated)
            {
                return;
            }

            Console.WriteLine(
                $"El mensaje {currentMessage.MessageId} " +
                $"ya no existe. Se creará nuevamente.");
        }

        IUserMessage newMessage =
            await channel.SendMessageAsync(
                embed: embed);

        await _repository.SaveCurrentMessageAsync(
            panel.PanelId,
            data.SeasonId,
            data.DayId,
            data.WaveId,
            newMessage.Id.ToString());

        if (currentMessage is not null &&
            currentMessage.WaveId != data.WaveId)
        {
            await TryFinalizePreviousMessageAsync(
                channel,
                currentMessage.MessageId);
        }

        Console.WriteLine(
            $"Nuevo panel Top 10 publicado: " +
            $"día {data.DayNumber}, " +
            $"wave {data.WaveNumber}.");
    }

    private static async Task<bool>
    TryUpdateMessageAsync(
        SocketTextChannel channel,
        string messageIdText,
        Embed embed)
    {
        if (!ulong.TryParse(
            messageIdText,
            out ulong messageId))
        {
            return false;
        }

        try
        {
            IUserMessage? message =
                await channel.GetMessageAsync(
                    messageId)
                as IUserMessage;

            if (message is null)
            {
                return false;
            }

            await message.ModifyAsync(
                properties =>
                {
                    properties.Embed = embed;
                });

            return true;
        }
        catch (global::Discord.Net.HttpException exception)
            when (exception.HttpCode ==
                System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static async Task
    TryFinalizePreviousMessageAsync(
        SocketTextChannel channel,
        string messageIdText)
    {
        if (!ulong.TryParse(
            messageIdText,
            out ulong messageId))
        {
            return;
        }

        try
        {
            IUserMessage? message =
                await channel.GetMessageAsync(
                    messageId)
                as IUserMessage;

            if (message is null)
            {
                return;
            }

            await message.ModifyAsync(
                properties =>
                {
                    properties.Content =
                        "✅ **Wave finalizada — " +
                        "resultado definitivo**";
                });
        }
        catch (global::Discord.Net.HttpException exception)
            when (exception.HttpCode ==
                System.Net.HttpStatusCode.NotFound)
        {
            // El mensaje fue eliminado manualmente.
        }
    }

    private static Embed BuildClanRankingEmbed(
        ClanRankingPanelData data)
    {
        StringBuilder description = new();

        foreach (ClanRankingPanelEntry clan
                 in data.Clans)
        {
            string medal =
                clan.Rank switch
                {
                    1 => "🥇",
                    2 => "🥈",
                    3 => "🥉",
                    _ => $"`{clan.Rank,2}.`"
                };

            string waveChange =
                FormatSignedNumber(
                    clan.WaveReputation);

            description.AppendLine(
                $"{medal} **{clan.ClanName}**");

            description.AppendLine(
                $"Total: `{clan.TotalReputation:N0}` " +
                $"• Wave: `{waveChange}`");

            if (clan.WaveDeduction > 0)
            {
                description.AppendLine(
                    $"Deducción en wave: " +
                    $"`-{clan.WaveDeduction:N0}`");
            }

            description.AppendLine();
        }

        string status =
            TranslateWaveStatus(
                data.WaveStatus);

        return new EmbedBuilder()
            .WithTitle(
                "🏆 Top 10 de clanes")

            .WithDescription(
                description.ToString())

            .WithColor(
                Color.Gold)

            .AddField(
                "Periodo",
                $"Día {data.DayNumber} • " +
                $"Wave {data.WaveNumber}",
                inline: true)

            .AddField(
                "Horario",
                $"{data.WaveStartTime:HH:mm} – " +
                $"{data.WaveEndTime:HH:mm} " +
                $"(servidor)",
                inline: true)

            .AddField(
                "Estado",
                status,
                inline: true)

            .WithFooter(
                $"{data.SeasonName} • " +
                $"Actualizado " +
                $"{data.LastUpdatedAt:HH:mm:ss}")

            .WithCurrentTimestamp()

            .Build();
    }

    private static string FormatSignedNumber(
        int value)
    {
        return value > 0
            ? $"+{value:N0}"
            : $"{value:N0}";
    }

    private static string TranslateWaveStatus(
        string status)
    {
        return status switch
        {
            "Pending" => "Pendiente",
            "InProgress" => "En progreso",
            "Complete" => "Finalizada",
            "Incomplete" => "Incompleta",
            "NoData" => "Sin datos",
            _ => status
        };
    }
}