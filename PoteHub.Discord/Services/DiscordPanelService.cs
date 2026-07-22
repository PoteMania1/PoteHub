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

    public const string ClanComparison =
    "ClanComparison";

    private static readonly TimeSpan
        UpdateInterval =
            TimeSpan.FromSeconds(5);

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

    public async Task<(string FirstClanName,
    string SecondClanName)>
    ConfigureComparisonAsync(
        ulong guildId,
        ulong channelId,
        int firstClanId,
        int secondClanId)
    {
        if (firstClanId == secondClanId)
        {
            throw new ArgumentException(
                "Tenés que seleccionar dos clanes " +
                "diferentes.");
        }

        string? firstClanName =
            await _repository.GetClanNameAsync(
                firstClanId);

        if (firstClanName is null)
        {
            throw new ArgumentException(
                $"No existe un clan con ID " +
                $"{firstClanId}.");
        }

        string? secondClanName =
            await _repository.GetClanNameAsync(
                secondClanId);

        if (secondClanName is null)
        {
            throw new ArgumentException(
                $"No existe un clan con ID " +
                $"{secondClanId}.");
        }

        await _repository.ConfigureComparisonAsync(
            guildId.ToString(),
            channelId.ToString(),
            firstClanId,
            secondClanId);

        return (
            firstClanName,
            secondClanName);
    }

    public async Task<bool> StopComparisonAsync(
        ulong guildId,
        ulong channelId)
    {
        return await _repository.StopComparisonAsync(
            guildId.ToString(),
            channelId.ToString());
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

            try
            {
                switch (panel.PanelType)
                {
                    case ClanRanking:
                        await UpdateClanRankingAsync(
                            client,
                            panel);
                        break;

                    case HomeClanMembers:
                        await UpdateHomeClanMembersAsync(
                            client,
                            panel);
                        break;

                    case ClanComparison:
                        await UpdateClanComparisonAsync(
                            client,
                            panel);
                        break;

                    default:
                        Console.WriteLine(
                            $"Tipo de panel desconocido: " +
                            $"{panel.PanelType}");
                        break;
                }
            }
            catch (global::Discord.Net.HttpException exception)
                when (IsTemporaryDiscordError(exception))
            {
                Console.WriteLine(
                    $"Discord no está disponible para el " +
                    $"panel {panel.PanelType}. Se probará " +
                    $"nuevamente en la próxima ronda.");
            }
            catch (HttpRequestException exception)
            {
                Console.WriteLine(
                    $"Error de conexión en el panel " +
                    $"{panel.PanelType}: " +
                    $"{exception.Message}");
            }
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

    private async Task UpdateHomeClanMembersAsync(
    DiscordSocketClient client,
    DiscordPanel panel)
    {
        if (panel.ClanId is null)
        {
            Console.WriteLine(
                $"El panel {panel.PanelId} no tiene " +
                "un clan configurado.");

            return;
        }

        MemberRankingPanelData? data =
            await _repository
                .GetCurrentMemberRankingAsync(
                    panel.ClanId.Value,
                    limit: 25);

        if (data is null ||
            data.Members.Count == 0)
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
            BuildMemberRankingEmbed(data);

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
                $"El mensaje " +
                $"{currentMessage.MessageId} " +
                "ya no existe. Se creará nuevamente.");
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
            currentMessage.WaveId !=
                data.WaveId)
        {
            await TryFinalizePreviousMessageAsync(
                channel,
                currentMessage.MessageId);
        }

        Console.WriteLine(
            $"Nuevo panel de miembros publicado: " +
            $"{data.ClanName}, " +
            $"día {data.DayNumber}, " +
            $"wave {data.WaveNumber}.");
    }

    private async Task UpdateClanComparisonAsync(
    DiscordSocketClient client,
    DiscordPanel panel)
    {
        if (panel.ClanId is null ||
            panel.ComparisonClanId is null)
        {
            Console.WriteLine(
                $"El panel {panel.PanelId} no tiene " +
                "los dos clanes configurados.");

            return;
        }

        ClanRankingPanelData? ranking =
            await _repository
                .GetCurrentClanRankingAsync(
                    limit: 100);

        if (ranking is null)
        {
            return;
        }

        ClanRankingPanelEntry? firstClan =
            ranking.Clans.FirstOrDefault(
                clan =>
                    clan.ClanId ==
                    panel.ClanId.Value);

        ClanRankingPanelEntry? secondClan =
            ranking.Clans.FirstOrDefault(
                clan =>
                    clan.ClanId ==
                    panel.ComparisonClanId.Value);

        if (firstClan is null ||
            secondClan is null)
        {
            return;
        }

        MemberRankingPanelData? firstMembers =
            await _repository
            .GetCurrentMemberRankingAsync(
            firstClan.ClanId,
            limit: 10,
            rankByWave: true);

        MemberRankingPanelData? secondMembers =
            await _repository
            .GetCurrentMemberRankingAsync(
             secondClan.ClanId,
             limit: 10,
                    rankByWave: true);

        if (firstMembers is null ||
            secondMembers is null)
        {
            return;
        }

        if (!ulong.TryParse(
            panel.ChannelId,
            out ulong channelId))
        {
            return;
        }

        SocketTextChannel? channel =
            client.GetChannel(channelId)
                as SocketTextChannel;

        if (channel is null)
        {
            return;
        }

        Embed embed =
            BuildClanComparisonEmbed(
                ranking,
                firstClan,
                secondClan,
                firstMembers,
                secondMembers);

        DiscordPanelMessage? currentMessage =
            await _repository
                .GetCurrentMessageAsync(
                    panel.PanelId);

        if (currentMessage is not null &&
            currentMessage.WaveId ==
                ranking.WaveId)
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
        }

        IUserMessage newMessage =
            await channel.SendMessageAsync(
                embed: embed);

        await _repository.SaveCurrentMessageAsync(
            panel.PanelId,
            ranking.SeasonId,
            ranking.DayId,
            ranking.WaveId,
            newMessage.Id.ToString());

        if (currentMessage is not null &&
            currentMessage.WaveId !=
                ranking.WaveId)
        {
            await TryFinalizePreviousMessageAsync(
                channel,
                currentMessage.MessageId);
        }

        Console.WriteLine(
            $"Comparación publicada: " +
            $"{firstClan.ClanName} vs " +
            $"{secondClan.ClanName}.");
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

        const int maximumAttempts = 3;

        for (int attempt = 1;
             attempt <= maximumAttempts;
             attempt++)
        {
            try
            {
                IUserMessage? message =
                    channel.GetCachedMessage(messageId)
                        as IUserMessage;

                message ??=
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
                // El mensaje fue eliminado de Discord.
                return false;
            }
            catch (global::Discord.Net.HttpException exception)
                when (IsTemporaryDiscordError(exception))
            {
                if (attempt == maximumAttempts)
                {
                    Console.WriteLine(
                        $"Discord no permitió actualizar " +
                        $"el mensaje {messageId} después " +
                        $"{maximumAttempts} intentos. " +
                        $"Se probará nuevamente en la " +
                        $"próxima actualización.");

                    // Devolvemos true para evitar que el
                    // servicio publique otro mensaje.
                    return true;
                }

                await Task.Delay(
                    GetRetryDelay(attempt));
            }
            catch (HttpRequestException exception)
            {
                if (attempt == maximumAttempts)
                {
                    Console.WriteLine(
                        $"Error temporal de conexión " +
                        $"actualizando el mensaje " +
                        $"{messageId}: " +
                        $"{exception.Message}");

                    return true;
                }

                await Task.Delay(
                    GetRetryDelay(attempt));
            }
            catch (TaskCanceledException)
            {
                if (attempt == maximumAttempts)
                {
                    Console.WriteLine(
                        $"Discord tardó demasiado en " +
                        $"actualizar el mensaje " +
                        $"{messageId}.");

                    return true;
                }

                await Task.Delay(
                    GetRetryDelay(attempt));
            }
        }

        return true;
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

        const int maximumAttempts = 3;

        for (int attempt = 1;
             attempt <= maximumAttempts;
             attempt++)
        {
            try
            {
                IUserMessage? message =
                    channel.GetCachedMessage(messageId)
                        as IUserMessage;

                message ??=
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

                return;
            }
            catch (global::Discord.Net.HttpException exception)
                when (exception.HttpCode ==
                      System.Net.HttpStatusCode.NotFound)
            {
                // El mensaje fue eliminado manualmente.
                return;
            }
            catch (global::Discord.Net.HttpException exception)
                when (IsTemporaryDiscordError(exception))
            {
                if (attempt == maximumAttempts)
                {
                    Console.WriteLine(
                        $"No se pudo finalizar el mensaje " +
                        $"{messageId}. Se continuará sin " +
                        $"detener el actualizador.");

                    return;
                }

                await Task.Delay(
                    GetRetryDelay(attempt));
            }
            catch (HttpRequestException exception)
            {
                if (attempt == maximumAttempts)
                {
                    Console.WriteLine(
                        $"Error de conexión finalizando " +
                        $"el mensaje {messageId}: " +
                        $"{exception.Message}");

                    return;
                }

                await Task.Delay(
                    GetRetryDelay(attempt));
            }
            catch (TaskCanceledException)
            {
                if (attempt == maximumAttempts)
                {
                    Console.WriteLine(
                        $"Discord tardó demasiado en " +
                        $"finalizar el mensaje " +
                        $"{messageId}.");

                    return;
                }

                await Task.Delay(
                    GetRetryDelay(attempt));
            }
        }
    }

    private static bool IsTemporaryDiscordError(
    global::Discord.Net.HttpException exception)
    {
        return exception.HttpCode is
            System.Net.HttpStatusCode.BadGateway or
            System.Net.HttpStatusCode.ServiceUnavailable or
            System.Net.HttpStatusCode.GatewayTimeout;
    }

    private static TimeSpan GetRetryDelay(
        int attempt)
    {
        return attempt switch
        {
            1 => TimeSpan.FromSeconds(1),
            2 => TimeSpan.FromSeconds(2),
            _ => TimeSpan.FromSeconds(4)
        };
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

    private static Embed BuildMemberRankingEmbed(
    MemberRankingPanelData data)
    {
        StringBuilder description = new();

        foreach (MemberRankingPanelEntry member
                 in data.Members)
        {
            string position =
                member.Rank switch
                {
                    1 => "🥇",
                    2 => "🥈",
                    3 => "🥉",
                    _ => $"`{member.Rank,2}.`"
                };

            string waveChange =
                FormatSignedNumber(
                    member.WaveNetReputation);

            string rewardStatus =
                member.TotalReputation >= 10000
                    ? "✅"
                    : "⏳";

            description.AppendLine(
                $"{position} {rewardStatus} " +
                $"**{member.MemberName}**");

            description.AppendLine(
                $"Rep: `{member.TotalReputation:N0}` " +
                $"• Wave: `{waveChange}` " +
                $"• Nv. `{member.Level}`");

            if (member.WaveReputationDeduction > 0)
            {
                description.AppendLine(
                    $"Deducción en wave: " +
                    $"`-{member.WaveReputationDeduction:N0}`");
            }

            description.AppendLine();
        }

        string status =
            TranslateWaveStatus(
                data.WaveStatus);

        return new EmbedBuilder()
            .WithTitle(
                $"🥷 Ranking de {data.ClanName}")

            .WithDescription(
                description.ToString())

            .WithColor(
                Color.Blue)

            .AddField(
                "Periodo",
                $"Día {data.DayNumber} • " +
                $"Wave {data.WaveNumber}",
                inline: true)

            .AddField(
                "Horario",
                $"{data.WaveStartTime:HH:mm} – " +
                $"{data.WaveEndTime:HH:mm} " +
                "(servidor)",
                inline: true)

            .AddField(
                "Estado",
                status,
                inline: true)

            .AddField(
                "Referencia",
                "✅ Alcanzó 10.000 de reputación\n" +
                "⏳ Todavía no alcanzó 10.000",
                inline: false)

            .WithFooter(
                $"{data.SeasonName} • " +
                $"Actualizado " +
                $"{data.LastUpdatedAt:HH:mm:ss}")

            .WithCurrentTimestamp()

            .Build();
    }

    private static Embed BuildClanComparisonEmbed(
    ClanRankingPanelData context,
    ClanRankingPanelEntry firstClan,
    ClanRankingPanelEntry secondClan,
    MemberRankingPanelData firstMembers,
    MemberRankingPanelData secondMembers)
    {
        int totalDifference =
            firstClan.TotalReputation -
            secondClan.TotalReputation;

        int waveDifference =
            firstClan.WaveReputation -
            secondClan.WaveReputation;

        string leader;

        if (waveDifference > 0)
        {
            leader =
                $"🏆 {firstClan.ClanName} lleva " +
                $"{Math.Abs(waveDifference):N0} " +
                "de ventaja en esta wave.";
        }
        else if (waveDifference < 0)
        {
            leader =
                $"🏆 {secondClan.ClanName} lleva " +
                $"{Math.Abs(waveDifference):N0} " +
                "de ventaja en esta wave.";
        }
        else
        {
            leader =
                "⚖️ Los dos clanes están empatados " +
                "en esta wave.";
        }

        string firstMemberList =
            BuildComparisonMemberList(
                firstMembers.Members);

        string secondMemberList =
            BuildComparisonMemberList(
                secondMembers.Members);

        string status =
            TranslateWaveStatus(
                context.WaveStatus);

        return new EmbedBuilder()
            .WithTitle(
                $"⚔️ {firstClan.ClanName} vs " +
                $"{secondClan.ClanName}")

            .WithDescription(leader)

            .WithColor(Color.DarkRed)

            .AddField(
                firstClan.ClanName,
                $"Puesto: `#{firstClan.Rank}`\n" +
                $"Total: `{firstClan.TotalReputation:N0}`\n" +
                $"Wave: `{FormatSignedNumber(firstClan.WaveReputation)}`\n" +
                $"Deducción: `{firstClan.WaveDeduction:N0}`",
                inline: true)

            .AddField(
                secondClan.ClanName,
                $"Puesto: `#{secondClan.Rank}`\n" +
                $"Total: `{secondClan.TotalReputation:N0}`\n" +
                $"Wave: `{FormatSignedNumber(secondClan.WaveReputation)}`\n" +
                $"Deducción: `{secondClan.WaveDeduction:N0}`",
                inline: true)

            .AddField(
                "Diferencia",
                $"Total: `{FormatSignedNumber(totalDifference)}`\n" +
                $"Wave: `{FormatSignedNumber(waveDifference)}`",
                inline: true)

            .AddField(
                $"Top miembros — " +
                $"{firstClan.ClanName}",
                firstMemberList,
                inline: true)

            .AddField(
                $"Top miembros — " +
                $"{secondClan.ClanName}",
                secondMemberList,
                inline: true)

            .AddField(
                "Periodo",
                $"Día {context.DayNumber} • " +
                $"Wave {context.WaveNumber}\n" +
                $"{context.WaveStartTime:HH:mm} – " +
                $"{context.WaveEndTime:HH:mm}\n" +
                $"Estado: {status}",
                inline: false)

            .WithFooter(
                $"{context.SeasonName} • " +
                $"Actualizado " +
                $"{context.LastUpdatedAt:HH:mm:ss}")

            .WithCurrentTimestamp()

            .Build();
    }

    private static string BuildComparisonMemberList(
        IEnumerable<MemberRankingPanelEntry> members)
    {
        StringBuilder builder = new();

        foreach (MemberRankingPanelEntry member
                 in members.Take(10))
        {
            builder.AppendLine(
                $"`{member.Rank,2}.` " +
                $"**{member.MemberName}**");

            builder.AppendLine(
                $"`{member.TotalReputation:N0}` " +
                $"• Wave " +
                $"`+{member.WaveReputationGain:N0}`");
        }

        return builder.Length == 0
            ? "Sin miembros disponibles."
            : builder.ToString();
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