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
            limit: 25,
            rankByWave: true);

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
            limit: 25,
            rankByWave: true);

        MemberRankingPanelData? secondMembers =
            await _repository
            .GetCurrentMemberRankingAsync(
             secondClan.ClanId,
             limit: 25,
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

        Embed summaryEmbed =
    BuildClanComparisonEmbed(
        ranking,
        firstClan,
        secondClan);

        Embed membersEmbed =
            BuildClanComparisonMembersEmbed(
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
            bool summaryUpdated =
                await TryUpdateMessageAsync(
                    channel,
                    currentMessage.MessageId,
                    summaryEmbed);

            bool membersUpdated = false;

            if (!string.IsNullOrWhiteSpace(
                currentMessage.SecondaryMessageId))
            {
                membersUpdated =
                    await TryUpdateMessageAsync(
                        channel,
                        currentMessage
                            .SecondaryMessageId,
                        membersEmbed);
            }

            if (summaryUpdated &&
                membersUpdated)
            {
                return;
            }

            string summaryMessageId =
                currentMessage.MessageId;

            string? membersMessageId =
                currentMessage.SecondaryMessageId;

            if (!summaryUpdated)
            {
                IUserMessage replacementSummary =
                    await channel.SendMessageAsync(
                        embed: summaryEmbed);

                summaryMessageId =
                    replacementSummary.Id.ToString();
            }

            if (!membersUpdated)
            {
                IUserMessage replacementMembers =
                    await channel.SendMessageAsync(
                        embed: membersEmbed);

                membersMessageId =
                    replacementMembers.Id.ToString();
            }

            await _repository.SaveCurrentMessageAsync(
                panel.PanelId,
                ranking.SeasonId,
                ranking.DayId,
                ranking.WaveId,
                summaryMessageId,
                membersMessageId);

            return;
        }

        IUserMessage newSummaryMessage =
            await channel.SendMessageAsync(
                embed: summaryEmbed);

        IUserMessage newMembersMessage =
            await channel.SendMessageAsync(
                embed: membersEmbed);

        await _repository.SaveCurrentMessageAsync(
            panel.PanelId,
            ranking.SeasonId,
            ranking.DayId,
            ranking.WaveId,
            newSummaryMessage.Id.ToString(),
            newMembersMessage.Id.ToString());

        if (currentMessage is not null &&
            currentMessage.WaveId !=
                ranking.WaveId)
        {
            await TryFinalizePreviousMessageAsync(
                channel,
                currentMessage.MessageId);

            if (!string.IsNullOrWhiteSpace(
                currentMessage.SecondaryMessageId))
            {
                await TryFinalizePreviousMessageAsync(
                    channel,
                    currentMessage
                        .SecondaryMessageId);
            }
        }


        Console.WriteLine(
            $"Comparación publicada en dos mensajes: " +
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

        double elapsedMinutes =
        (
            data.LastUpdatedAt -
            data.WaveStartTime
        ).TotalMinutes;

        elapsedMinutes = Math.Clamp(
            elapsedMinutes,
            1,
            30);

        foreach (ClanRankingPanelEntry clan
         in data.Clans)
        {
            string waveChange =
                FormatSignedNumber(
                    clan.WaveReputation);

            double reputationPerMinute =
                Math.Max(
                    0,
                    clan.WaveReputation) /
                elapsedMinutes;

            description.AppendLine(
                $"**#{clan.Rank} {clan.ClanName}** — " +
                $"`{waveChange} rep` • " +
                $"`{reputationPerMinute:N1}/min`");

            if (clan.WaveDeduction > 0)
            {
                description.AppendLine(
                    $"↳ Deducción: " +
                    $"`-{clan.WaveDeduction:N0}`");
            }

            description.AppendLine();
        }

        string status =
            TranslateWaveStatus(
                data.WaveStatus);

        return new EmbedBuilder()
            .WithTitle(
                $"⚔️ Actividad de clanes — " +
                $"Wave {data.WaveNumber}")

            .WithDescription(
                description.ToString())

            .WithColor(
                new Color(
                    52,
                    152,
                    219))

            .AddField(
                "Día",
                data.DayNumber,
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
                $"Última actualización " +
                $"{data.LastUpdatedAt:HH:mm:ss}")

            .WithCurrentTimestamp()

            .Build();
    }

    private static Embed BuildMemberRankingEmbed(
    MemberRankingPanelData data)
    {
        StringBuilder description = new();

        double elapsedMinutes =
        data.WaveStatus is "Complete" or "Incomplete"
        ? 30
        : (
            data.LastUpdatedAt -
            data.WaveStartTime
          ).TotalMinutes;

        elapsedMinutes = Math.Clamp(
            elapsedMinutes,
            1,
            30);

        foreach (MemberRankingPanelEntry member
                 in data.Members)
        {
            double reputationPerMinute =
                member.WaveReputationGain /
                elapsedMinutes;

            string waveChange =
                FormatSignedNumber(
                    member.WaveNetReputation);

            description.AppendLine(
                $"🥷 **{member.MemberName}** — " +
                $"`{waveChange} rep` • " +
                $"`{reputationPerMinute:N1}/min`");

            if (member.WaveReputationDeduction > 0)
            {
                description.AppendLine(
                    $"Ganada: " +
                    $"`+{member.WaveReputationGain:N0}` " +
                    $"• Deducción: " +
                    $"`-{member.WaveReputationDeduction:N0}`");
            }

            description.AppendLine();
        }

        string status =
            TranslateWaveStatus(
                data.WaveStatus);

        return new EmbedBuilder()
            .WithTitle(
                $"⚔️ Actividad de {data.ClanName} — " +
                $"Wave {data.WaveNumber}")

            .WithDescription(
                description.ToString())

            .WithColor(
                new Color(
                    52,
                    152,
                    219))

            .AddField(
                "Día",
                data.DayNumber,
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
                $"Última actualización " +
                $"{data.LastUpdatedAt:HH:mm:ss}")

            .WithCurrentTimestamp()

            .Build();
    }

    private static Embed BuildClanComparisonEmbed(
    ClanRankingPanelData context,
    ClanRankingPanelEntry firstClan,
    ClanRankingPanelEntry secondClan)
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

        string status =
            TranslateWaveStatus(
                context.WaveStatus);

        return new EmbedBuilder()
            .WithTitle(
                $"⚔️ {firstClan.ClanName} vs " +
                $"{secondClan.ClanName}")

            .WithDescription(
                leader)

            .WithColor(
                Color.DarkRed)

            .AddField(
                firstClan.ClanName,

                $"Puesto: `#{firstClan.Rank}`\n" +
                $"Total: `{firstClan.TotalReputation:N0}`\n" +
                $"Wave: `{FormatSignedNumber(
                    firstClan.WaveReputation)}`\n" +
                $"Deducción: " +
                $"`{firstClan.WaveDeduction:N0}`",

                inline: true)

            .AddField(
                secondClan.ClanName,

                $"Puesto: `#{secondClan.Rank}`\n" +
                $"Total: `{secondClan.TotalReputation:N0}`\n" +
                $"Wave: `{FormatSignedNumber(
                    secondClan.WaveReputation)}`\n" +
                $"Deducción: " +
                $"`{secondClan.WaveDeduction:N0}`",

                inline: true)

            .AddField(
                "Diferencia",

                $"Total: `{FormatSignedNumber(
                    totalDifference)}`\n" +
                $"Wave: `{FormatSignedNumber(
                    waveDifference)}`",

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

    private static Embed
    BuildClanComparisonMembersEmbed(
        ClanRankingPanelData context,
        ClanRankingPanelEntry firstClan,
        ClanRankingPanelEntry secondClan,
        MemberRankingPanelData firstMembers,
        MemberRankingPanelData secondMembers)
    {
        double elapsedMinutes =
            context.WaveStatus is "Complete" or "Incomplete"
            ? 30
            : (
                context.LastUpdatedAt -
                context.WaveStartTime
              ).TotalMinutes;

        elapsedMinutes = Math.Clamp(
            elapsedMinutes,
            1,
            30);

        string firstMemberList =
        BuildComparisonMemberList(
        firstMembers.Members.Take(25),
        elapsedMinutes,
        startPosition: 1);

        string secondMemberList =
            BuildComparisonMemberList(
                secondMembers.Members.Take(25),
                elapsedMinutes,
                startPosition: 1);

        return new EmbedBuilder()
            .WithTitle(
                $"🥷 Miembros — " +
                $"{firstClan.ClanName} vs " +
                $"{secondClan.ClanName}")

            .WithColor(
                Color.DarkRed)

            .AddField(
                firstClan.ClanName,
                firstMemberList,
                inline: true)
            
            .AddField(
                secondClan.ClanName,
                secondMemberList,
                inline: true)

            .WithFooter(
                $"Día {context.DayNumber} • " +
                $"Wave {context.WaveNumber} • " +
                $"Actualizado " +
                $"{context.LastUpdatedAt:HH:mm:ss}")

            .WithCurrentTimestamp()

            .Build();
    }

    private static string ShortenComparisonName(
    string name)
    {
        const int maximumLength = 15;

        string cleanName = name
            .Replace("\r", " ")
            .Replace("\n", " ");

        if (cleanName.Length <= maximumLength)
        {
            return cleanName;
        }

        return cleanName[
            ..(maximumLength - 1)] + "…";
    }

    private static string BuildComparisonMemberList(
    IEnumerable<MemberRankingPanelEntry> members,
    double elapsedMinutes,
    int startPosition)
    {
        StringBuilder builder = new();

        int position = startPosition;

        foreach (MemberRankingPanelEntry member
                 in members)
        {
            double reputationPerMinute =
                member.WaveReputationGain /
                elapsedMinutes;

            string waveChange =
                FormatSignedNumber(
                    member.WaveNetReputation);

            string memberName =
                ShortenComparisonName(
                    member.MemberName);

            builder.AppendLine(
                $"`{position,2}.` " +
                $"**{memberName}** " +
                $"`{waveChange}` " +
                $"`{reputationPerMinute:N1}/m`");

            position++;
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