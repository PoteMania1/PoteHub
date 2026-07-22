using Discord;
using Discord.WebSocket;
using Microsoft.Data.Sqlite;
using PoteHub.Database.Data;
using PoteHub.Database.RepositoryBase;
using PoteHub.Domain.Entities;
using PoteHub.Domain.Enums;
using System.Text;

namespace PoteHub.Discord.Services;

public class DiscordBotService
{
    private readonly DiscordSocketClient _client;
    private readonly DiscordUserRepository
    _discordUserRepository;
    private readonly DiscordPanelService
    _panelService;
    private readonly DatabaseConnection _database;

    private readonly StatisticsRepository
        _statisticsRepository;

    private readonly DiscordPanelRepository
        _panelRepository;
    private readonly DiscordWaveReportRepository
    _waveReportRepository;

    private readonly DiscordWaveReportService
        _waveReportService;

    private Task? _waveReportTask;
    private Task? _panelUpdaterTask;
    private CancellationToken
        _shutdownToken;


    public async Task StartAsync(
    string token,
    CancellationToken cancellationToken)
    {
        _shutdownToken =
            cancellationToken;

        await _client.LoginAsync(
                TokenType.Bot,
            token);

        await _client.StartAsync();

        try
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            // Cierre solicitado por el usuario.
        }

        if (_panelUpdaterTask is not null)
        {
            try
            {
                await _panelUpdaterTask;
            }
            catch (OperationCanceledException)
            {
                // Finalización normal.
            }
        }

        if (_waveReportTask is not null)
        {
            try
            {
                await _waveReportTask;
            }
            catch (OperationCanceledException)
            {
                // Finalización normal.
            }
        }

        await _client.StopAsync();
        await _client.LogoutAsync();

        await _client.DisposeAsync();
    }

    private static Task OnLogAsync(
    LogMessage message)
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] " +
            $"[{message.Severity}] " +
            message.Message);

        if (message.Exception is not null)
        {
            Console.WriteLine(
                message.Exception);
        }

        return Task.CompletedTask;
    }
    private async Task OnReadyAsync()
    {
        Console.ForegroundColor =
            ConsoleColor.Green;

        Console.WriteLine();
        Console.WriteLine(
            $"PoteHub conectado como " +
            $"{_client.CurrentUser.Username}.");

        Console.WriteLine(
            $"Servidores conectados: " +
            $"{_client.Guilds.Count}");

        Console.ResetColor();

        SlashCommandBuilder pingCommand = new()
        {
            Name = "ping",
            Description =
                "Comprueba si PoteHub está funcionando."
        };

        SlashCommandBuilder linkCommand = new()
        {
            Name = "vincular",
            Description =
        "Vincula tu Discord con un personaje."
        };

        SlashCommandBuilder myCharacterCommand = new()
        {
            Name = "mi-personaje",
            Description =
        "Muestra la información de tu personaje."
        };

        SlashCommandBuilder configurePanelCommand =
    new()
    {
        Name = "configurar-panel",
        Description =
            "Configura un panel público de PoteHub."
    };

        configurePanelCommand.AddOption(
            new SlashCommandOptionBuilder()
                .WithName("tipo")
                .WithDescription(
                    "Panel que querés configurar")
                .WithRequired(true)
                .WithType(
                    ApplicationCommandOptionType.String)
                .AddChoice(
                    "Ranking de clanes",
                    "ranking-clanes")
                .AddChoice(
                    "Miembros de mi clan",
                    "miembros-clan"));

        configurePanelCommand.AddOption(
            name: "canal",
            type: ApplicationCommandOptionType.Channel,
            description:
                "Canal donde se publicará el panel",
            isRequired: true);

        configurePanelCommand.AddOption(
            name: "clan_id",
            type: ApplicationCommandOptionType.Integer,
            description:
                "ID del clan para el panel de miembros",
            isRequired: false,
            minValue: 1);

        SlashCommandBuilder rankingCommand = new()
        {
            Name = "ranking",
            Description =
        "Muestra un ranking de jugadores."
        };

        rankingCommand.AddOption(
            new SlashCommandOptionBuilder()
                .WithName("tipo")
                .WithDescription(
                    "Ranking que querés consultar")
                .WithRequired(true)
                .WithType(
                    ApplicationCommandOptionType.String)
                .AddChoice(
                    "Global de temporada",
                    "global")
                .AddChoice(
                    "Día actual",
                    "diario")
                .AddChoice(
                    "Wave actual",
                    "wave")
                .AddChoice(
                    "Último día disponible",
                    "ultimo-dia"));

        rankingCommand.AddOption(
            name: "limite",
            type:
                ApplicationCommandOptionType.Integer,
            description:
                "Cantidad de jugadores, entre 1 y 25",
            isRequired: false,
            minValue: 1,
            maxValue: 25);

        SlashCommandBuilder compareCommand = new()
        {
            Name = "comparar-clanes",
            Description =
                "Compara dos clanes en vivo."
        };

        compareCommand.AddOption(
            name: "clan_1",
            type:
                ApplicationCommandOptionType.Integer,
            description:
                "ID del primer clan",
            isRequired: true,
            minValue: 1);

        compareCommand.AddOption(
            name: "clan_2",
            type:
                ApplicationCommandOptionType.Integer,
            description:
                "ID del segundo clan",
            isRequired: true,
            minValue: 1);

        SlashCommandBuilder stopComparisonCommand =
            new()
            {
                Name = "detener-seguimiento",
                Description =
                    "Detiene la comparación del canal."
            };

        linkCommand.AddOption(
            name: "personaje_id",
            type: ApplicationCommandOptionType.Integer,
            description:
                "ID único de tu personaje",
            isRequired: true,
            minValue: 1);

        SlashCommandBuilder publishCharacterCommand =
    new()
    {
        Name = "publicar-mi-personaje",
        Description =
            "Publica el panel fijo Ver mi personaje."
    };

        publishCharacterCommand.AddOption(
            name: "canal",
            type: ApplicationCommandOptionType.Channel,
            description:
                "Canal donde se publicará el panel",
            isRequired: true);

        SlashCommandBuilder configureReportsCommand =
            new()
            {
                Name = "configurar-reportes",
                Description =
                    "Configura los reportes automáticos."
            };

        configureReportsCommand.AddOption(
            name: "canal",
            type: ApplicationCommandOptionType.Channel,
            description:
                "Canal de reportes",
            isRequired: true);

        configureReportsCommand.AddOption(
            name: "clan_id",
            type: ApplicationCommandOptionType.Integer,
            description:
                "ID del clan que se incluirá",
            isRequired: true,
            minValue: 1);

        foreach (SocketGuild guild in _client.Guilds)
        {
            ApplicationCommandProperties[] commands =
            [
                pingCommand.Build(),
                linkCommand.Build(),
                myCharacterCommand.Build(),
                configurePanelCommand.Build(),
                rankingCommand.Build(),
                compareCommand.Build(),
                stopComparisonCommand.Build(),
                publishCharacterCommand.Build(),
                configureReportsCommand.Build(),
            ];

            await guild.BulkOverwriteApplicationCommandAsync(
                commands);

            Console.WriteLine(
                $"Comandos de PoteHub sincronizados " +
                $"en {guild.Name}.");
        }

        if (_panelUpdaterTask is null)
        {
            _panelUpdaterTask =
                _panelService.RunUpdaterAsync(
                    _client,
                    _shutdownToken);
        }

        if (_waveReportTask is null)
        {
            _waveReportTask =
                _waveReportService.RunAsync(
                    _client,
                    _shutdownToken);
        }

    }

    public DiscordBotService(
    DatabaseConnection database)
    {
        DiscordSocketConfig configuration = new()
        {
            GatewayIntents = GatewayIntents.Guilds,
            LogGatewayIntentWarnings = false
        };

        _client = new DiscordSocketClient(
            configuration);

        _client.Log += OnLogAsync;
        _client.Ready += OnReadyAsync;
        _client.SlashCommandExecuted +=
        OnSlashCommandExecutedAsync;
        _client.ButtonExecuted +=
        OnButtonExecutedAsync;
        _database = database;

        _statisticsRepository =
            new StatisticsRepository(database);

        _panelRepository =
            new DiscordPanelRepository(database);
        _waveReportRepository =
        new DiscordWaveReportRepository(database);

        _waveReportService =
            new DiscordWaveReportService(
                _waveReportRepository);
        _discordUserRepository =
        new DiscordUserRepository(database);
        _panelService =
    new DiscordPanelService(
        _panelRepository);
    }

    private async Task
    OnSlashCommandExecutedAsync(
        SocketSlashCommand command)
    {
        try
        {
            switch (command.CommandName)
            {
                case "ping":
                    await HandlePingAsync(command);
                    break;

                case "vincular":
                    await HandleLinkAsync(command);
                    break;
                case "mi-personaje":
                    await HandleMyCharacterAsync(command);
                    break;
                case "configurar-panel":
                    await HandleConfigurePanelAsync(
                        command);
                    break;
                case "ranking":
                    await HandleRankingAsync(command);
                    break;

                case "comparar-clanes":
                    await HandleCompareClansAsync(
                        command);
                    break;

                case "detener-seguimiento":
                    await HandleStopComparisonAsync(
                        command);
                    break;
                case "publicar-mi-personaje":
                    await HandlePublishCharacterAsync(
                        command);
                    break;

                case "configurar-reportes":
                    await HandleConfigureReportsAsync(
                        command);
                    break;
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);

            if (!command.HasResponded)
            {
                await command.RespondAsync(
                    "Ocurrió un error procesando " +
                    "el comando.",
                    ephemeral: true);
            }
            else
            {
                await command.FollowupAsync(
                    "Ocurrió un error procesando " +
                    "el comando.",
                    ephemeral: true);
            }
        }
    }

    private static async Task HandlePingAsync(
    SocketSlashCommand command)
    {
        await command.RespondAsync(
            "🏓 PoteHub está funcionando correctamente.",
            ephemeral: true);
    }

    private async Task HandleLinkAsync(
    SocketSlashCommand command)
    {
        await command.DeferAsync(
            ephemeral: true);

        SocketSlashCommandDataOption? option =
            command.Data.Options.FirstOrDefault(
                item =>
                    item.Name == "personaje_id");

        if (option?.Value is null)
        {
            await command.FollowupAsync(
                "No se recibió el ID del personaje.",
                ephemeral: true);

            return;
        }

        int memberId =
            Convert.ToInt32(option.Value);

        string discordId =
            command.User.Id.ToString();

        DiscordLinkResult result =
            await _discordUserRepository.LinkAsync(
                discordId,
                memberId);

        switch (result.Status)
        {
            case DiscordLinkStatus.MemberNotFound:
                await command.FollowupAsync(
                    $"No encontré ningún personaje " +
                    $"con el ID {memberId}.",
                    ephemeral: true);
                break;

            case DiscordLinkStatus
                .MemberAlreadyLinked:

                await command.FollowupAsync(
                    "Ese personaje ya está vinculado " +
                    "a otra cuenta de Discord.",
                    ephemeral: true);
                break;

            case DiscordLinkStatus.Success:
                await command.FollowupAsync(
                    $"✅ Vinculación completada.\n" +
                    $"Personaje: **{result.MemberName}**\n" +
                    $"Clan: **{result.ClanName}**\n" +
                    $"ID: `{result.MemberId}`",
                    ephemeral: true);
                break;
        }
    }

    private async Task HandleConfigurePanelAsync(
    SocketSlashCommand command)
    {
        if (command.User is not SocketGuildUser
            guildUser)
        {
            await command.RespondAsync(
                "Este comando solamente funciona " +
                "dentro de un servidor.",
                ephemeral: true);

            return;
        }

        if (!guildUser.GuildPermissions
            .ManageGuild)
        {
            await command.RespondAsync(
                "Necesitás el permiso Administrar " +
                "servidor para configurar paneles.",
                ephemeral: true);

            return;
        }

        SocketSlashCommandDataOption? typeOption =
            command.Data.Options.FirstOrDefault(
                option => option.Name == "tipo");

        SocketSlashCommandDataOption? channelOption =
            command.Data.Options.FirstOrDefault(
                option => option.Name == "canal");

        SocketSlashCommandDataOption? clanOption =
            command.Data.Options.FirstOrDefault(
                option => option.Name == "clan_id");

        string? panelType =
            typeOption?.Value?.ToString();

        if (channelOption?.Value
            is not SocketTextChannel channel)
        {
            await command.RespondAsync(
                "Debés elegir un canal de texto.",
                ephemeral: true);

            return;
        }

        await command.DeferAsync(
            ephemeral: true);

        if (panelType == "ranking-clanes")
        {
            await _panelService
                .ConfigureClanRankingAsync(
                    guildUser.Guild.Id,
                    channel.Id);

            await command.FollowupAsync(
                $"✅ Panel de ranking de clanes " +
                $"configurado en {channel.Mention}.",
                ephemeral: true);

            return;
        }

        if (panelType == "miembros-clan")
        {
            if (clanOption?.Value is null)
            {
                await command.FollowupAsync(
                    "Para el panel de miembros " +
                    "tenés que indicar `clan_id`.",
                    ephemeral: true);

                return;
            }

            int clanId =
                Convert.ToInt32(
                    clanOption.Value);

            try
            {
                string clanName =
                    await _panelService
                        .ConfigureHomeClanAsync(
                            guildUser.Guild.Id,
                            channel.Id,
                            clanId);

                await command.FollowupAsync(
                    $"✅ Panel de **{clanName}** " +
                    $"configurado en " +
                    $"{channel.Mention}.",
                    ephemeral: true);
            }
            catch (ArgumentException exception)
            {
                await command.FollowupAsync(
                    exception.Message,
                    ephemeral: true);
            }

            return;
        }

        await command.FollowupAsync(
            "El tipo de panel no es válido.",
            ephemeral: true);
    }

    private async Task HandlePublishCharacterAsync(
    SocketSlashCommand command)
    {
        if (command.User is not SocketGuildUser
            guildUser)
        {
            await command.RespondAsync(
                "Este comando solamente funciona " +
                "dentro de un servidor.",
                ephemeral: true);

            return;
        }

        if (!guildUser.GuildPermissions
            .ManageGuild)
        {
            await command.RespondAsync(
                "Necesitás el permiso Administrar " +
                "servidor.",
                ephemeral: true);

            return;
        }

        SocketSlashCommandDataOption? channelOption =
            command.Data.Options.FirstOrDefault(
                option => option.Name == "canal");

        if (channelOption?.Value
            is not SocketTextChannel channel)
        {
            await command.RespondAsync(
                "Debés elegir un canal de texto.",
                ephemeral: true);

            return;
        }

        Embed embed =
            new EmbedBuilder()
                .WithTitle(
                    "🥷 Información de tu personaje")
                .WithDescription(
                    "Presioná el botón para consultar " +
                    "tu personaje vinculado.\n\n" +
                    "La respuesta será privada y " +
                    "solamente podrás verla vos.")
                .WithColor(Color.Purple)
                .WithFooter("PoteHub")
                .Build();

        MessageComponent components =
            new ComponentBuilder()
                .WithButton(
                    label: "Ver mi personaje",
                    customId: "show_my_character",
                    style: ButtonStyle.Primary,
                    emote: new Emoji("🥷"))
                .Build();

        await channel.SendMessageAsync(
            embed: embed,
            components: components);

        await command.RespondAsync(
            $"✅ Panel publicado en " +
            $"{channel.Mention}.",
            ephemeral: true);
    }

    private async Task HandleConfigureReportsAsync(
        SocketSlashCommand command)
    {
        if (command.User is not SocketGuildUser
            guildUser)
        {
            await command.RespondAsync(
                "Este comando solamente funciona " +
                "dentro de un servidor.",
                ephemeral: true);

            return;
        }

        if (!guildUser.GuildPermissions
            .ManageGuild)
        {
            await command.RespondAsync(
                "Necesitás el permiso Administrar " +
                "servidor.",
                ephemeral: true);

            return;
        }

        SocketSlashCommandDataOption? channelOption =
            command.Data.Options.FirstOrDefault(
                option => option.Name == "canal");

        SocketSlashCommandDataOption? clanOption =
            command.Data.Options.FirstOrDefault(
                option => option.Name == "clan_id");

        if (channelOption?.Value
            is not SocketTextChannel channel ||
            clanOption?.Value is null)
        {
            await command.RespondAsync(
                "Tenés que indicar un canal y un clan.",
                ephemeral: true);

            return;
        }

        int clanId =
            Convert.ToInt32(clanOption.Value);

        string? clanName =
            await _panelRepository
                .GetClanNameAsync(clanId);

        if (clanName is null)
        {
            await command.RespondAsync(
                $"No existe un clan con ID {clanId}.",
                ephemeral: true);

            return;
        }

        await _waveReportRepository.ConfigureAsync(
            guildUser.Guild.Id.ToString(),
            channel.Id.ToString(),
            clanId);

        await command.RespondAsync(
            $"✅ Los reportes de **{clanName}** " +
            $"se publicarán en {channel.Mention} " +
            $"al terminar cada wave.",
            ephemeral: true);
    }

    private async Task HandleMyCharacterAsync(
    SocketSlashCommand command)
    {
        await command.DeferAsync(
            ephemeral: true);

        string discordId =
            command.User.Id.ToString();

        MemberProfile? profile =
            await _discordUserRepository
                .GetProfileAsync(discordId);

        if (profile is null)
        {
            await command.FollowupAsync(
                "No tenés ningún personaje vinculado.\n" +
                "Usá `/vincular personaje_id:` " +
                "para vincularlo.",
                ephemeral: true);

            return;
        }

        Embed embed =
            BuildProfileEmbed(profile);

        MessageComponent components =
            BuildProfileComponents();

        await command.FollowupAsync(
            embed: embed,
            components: components,
            ephemeral: true);
    }

    private static Embed BuildProfileEmbed(
    MemberProfile profile)
    {
        string rewardStatus =
            profile.IsRewardEligible
                ? "✅ Clasificado para premios"
                : "❌ Todavía no clasificado";

        string progressBar =
            CreateProgressBar(
                profile.ProgressPercentage);

        string waveChange =
            profile.WaveNetReputation > 0
                ? $"+{profile.WaveNetReputation:N0}"
                : $"{profile.WaveNetReputation:N0}";

        string waveStatus =
            profile.WaveStatus switch
            {
                "Pending" => "Pendiente",
                "InProgress" => "En progreso",
                "Complete" => "Completa",
                "Incomplete" => "Incompleta",
                "NoData" => "Sin datos",
                _ => profile.WaveStatus
            };

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle(
                $"🥷 {profile.MemberName}")

            .WithColor(
                profile.IsRewardEligible
                    ? Color.Green
                    : Color.Orange)

            .AddField(
                "Clan",
                profile.ClanName,
                inline: true)

            .AddField(
                "Nivel",
                profile.Level,
                inline: true)

            .AddField(
                "Ranking global",
                $"#{profile.GlobalRank}",
                inline: true)

            .AddField(
                "Reputación",
                $"{profile.CurrentReputation:N0} / " +
                $"{profile.RequiredReputation:N0}",
                inline: false)

            .AddField(
                "Progreso",
                $"{progressBar}\n" +
                $"{profile.ProgressPercentage:N2}%",
                inline: false)

            .AddField(
                "Premios",
                rewardStatus,
                inline: false)

            .AddField(
                "Wave actual",
                $"Día {profile.DayNumber} • " +
                $"Wave {profile.WaveNumber}\n" +
                $"Estado: **{waveStatus}**",
                inline: false)

            .AddField(
                "Horario de la wave",
                $"{profile.WaveStartTime:HH:mm} – " +
                $"{profile.WaveEndTime:HH:mm} " +
                $"(hora del servidor)",
                inline: true)

            .AddField(
                "Reputación en esta wave",
                $"Ganada: **+{profile.WaveReputationGain:N0}**\n" +
                $"Deducida: **-{profile.WaveReputationDeduction:N0}**\n" +
                $"Resultado: **{waveChange}**",
                inline: true)

            .AddField(
                "Última actualización",
                $"{profile.LastUpdatedAt:dd/MM/yyyy HH:mm:ss} " +
                $"(servidor)",
                inline: false)

            .WithFooter(
                $"{profile.SeasonName} • " +
                $"ID {profile.MemberId}")

            .WithCurrentTimestamp();

        if (!profile.IsRewardEligible)
        {
            embed.AddField(
                "Reputación faltante",
                $"{profile.RemainingReputation:N0}",
                inline: false);
        }

        return embed.Build();
    }

    private static MessageComponent
    BuildProfileComponents()
    {
        ButtonBuilder refreshButton = new()
        {
            CustomId = "refresh_my_character",
            Label = "Actualizar",
            Emote = new Emoji("🔄"),
            Style = ButtonStyle.Primary
        };

        return new ComponentBuilder()
            .WithButton(refreshButton)
            .Build();
    }

    private async Task OnButtonExecutedAsync(
    SocketMessageComponent component)
    {
        try
        {
            if (component.Data.CustomId ==
                "show_my_character")
            {
                string discordId =
                    component.User.Id.ToString();

                MemberProfile? profile =
                    await _discordUserRepository
                        .GetProfileAsync(discordId);

                if (profile is null)
                {
                    await component.RespondAsync(
                        "No tenés ningún personaje " +
                        "vinculado.\nUsá `/vincular`.",
                        ephemeral: true);

                    return;
                }

                await component.RespondAsync(
                    embed: BuildProfileEmbed(profile),
                    components:
                        BuildProfileComponents(),
                    ephemeral: true);

                return;
            }

            if (component.Data.CustomId ==
                "refresh_my_character")
            {
                string discordId =
                    component.User.Id.ToString();

                MemberProfile? profile =
                    await _discordUserRepository
                        .GetProfileAsync(discordId);

                if (profile is null)
                {
                    await component.RespondAsync(
                        "No tenés ningún personaje " +
                        "vinculado.",
                        ephemeral: true);

                    return;
                }

                Embed embed =
                    BuildProfileEmbed(profile);

                MessageComponent components =
                    BuildProfileComponents();

                await component.UpdateAsync(
                    properties =>
                    {
                        properties.Embed = embed;
                        properties.Components =
                            components;
                    });
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);

            if (!component.HasResponded)
            {
                await component.RespondAsync(
                    "No se pudo actualizar la información.",
                    ephemeral: true);
            }
        }
    }

    private async Task HandleRankingAsync(
    SocketSlashCommand command)
    {
        await command.DeferAsync(
            ephemeral: true);

        string rankingType =
            command.Data.Options
                .First(option =>
                    option.Name == "tipo")
                .Value
                .ToString()!;

        SocketSlashCommandDataOption? limitOption =
            command.Data.Options.FirstOrDefault(
                option =>
                    option.Name == "limite");

        int limit = limitOption?.Value is null
            ? 10
            : Convert.ToInt32(
                limitOption.Value);

        ClanRankingPanelData? context =
            await _panelRepository
                .GetCurrentClanRankingAsync(
                    limit: 1);

        if (context is null)
        {
            await command.FollowupAsync(
                "Todavía no hay datos sincronizados.",
                ephemeral: true);

            return;
        }

        using SqliteConnection connection =
            _database.CreateConnection();

        await connection.OpenAsync();

        List<MemberRankingEntry> ranking;

        string title;

        switch (rankingType)
        {
            case "global":
                ranking =
                    await _statisticsRepository
                        .GetGlobalRankingAsync(
                            context.SeasonId,
                            limit,
                            connection);

                title =
                    "🌍 Ranking global";
                break;

            case "diario":
                ranking =
                    await _statisticsRepository
                        .GetDailyRankingAsync(
                            context.SeasonId,
                            context.DayNumber,
                            limit,
                            connection);

                title =
                    $"📅 Ranking del día " +
                    $"{context.DayNumber}";
                break;

            case "wave":
                ranking =
                    await _statisticsRepository
                        .GetWaveRankingAsync(
                            context.WaveId,
                            limit,
                            connection);

                title =
                    $"🌊 Ranking de la wave " +
                    $"{context.WaveNumber}";
                break;

            case "ultimo-dia":
                ranking =
                    await _statisticsRepository
                        .GetLastAvailableDayRankingAsync(
                            context.SeasonId,
                            limit,
                            connection);

                title =
                    "🏁 Ranking del último día";
                break;

            default:
                await command.FollowupAsync(
                    "El tipo de ranking no es válido.",
                    ephemeral: true);

                return;
        }

        Embed embed =
            BuildPlayerRankingEmbed(
                title,
                context,
                ranking);

        await command.FollowupAsync(
            embed: embed,
            ephemeral: true);
    }

    private static Embed BuildPlayerRankingEmbed(
        string title,
        ClanRankingPanelData context,
        IEnumerable<MemberRankingEntry> ranking)
    {
        StringBuilder description = new();

        foreach (MemberRankingEntry member
                 in ranking)
        {
            string position =
                member.Rank switch
                {
                    1 => "🥇",
                    2 => "🥈",
                    3 => "🥉",
                    _ => $"`{member.Rank,2}.`"
                };

            description.AppendLine(
                $"{position} **{member.MemberName}**");

            description.AppendLine(
                $"Clan: `{member.ClanName}` • " +
                $"Rep: `{member.CurrentReputation:N0}`");

            if (member.ReputationGain != 0 ||
                member.ReputationDeduction != 0)
            {
                description.AppendLine(
                    $"Ganada: `+{member.ReputationGain:N0}` " +
                    $"• Deducida: " +
                    "`-" +
                    $"{member.ReputationDeduction:N0}`");
            }

            description.AppendLine();
        }

        if (description.Length == 0)
        {
            description.Append(
                "Todavía no hay actividad registrada.");
        }

        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(
                description.ToString())
            .WithColor(Color.Purple)
            .WithFooter(
                $"{context.SeasonName} • " +
                $"Día {context.DayNumber} • " +
                $"Wave {context.WaveNumber}")
            .WithCurrentTimestamp()
            .Build();
    }

    private async Task HandleCompareClansAsync(
    SocketSlashCommand command)
    {
        if (command.User is not SocketGuildUser
            guildUser)
        {
            await command.RespondAsync(
                "Este comando solamente funciona " +
                "dentro de un servidor.",
                ephemeral: true);

            return;
        }

        if (!guildUser.GuildPermissions
            .ManageGuild)
        {
            await command.RespondAsync(
                "Necesitás el permiso Administrar " +
                "servidor para iniciar seguimientos.",
                ephemeral: true);

            return;
        }

        if (command.Channel
            is not SocketTextChannel channel)
        {
            await command.RespondAsync(
                "Este comando debe ejecutarse en " +
                "un canal de texto.",
                ephemeral: true);

            return;
        }

        int firstClanId =
            Convert.ToInt32(
                command.Data.Options
                    .First(option =>
                        option.Name == "clan_1")
                    .Value);

        int secondClanId =
            Convert.ToInt32(
                command.Data.Options
                    .First(option =>
                        option.Name == "clan_2")
                    .Value);

        await command.DeferAsync(
            ephemeral: true);

        try
        {
            (string firstClanName,
             string secondClanName) =
                await _panelService
                    .ConfigureComparisonAsync(
                        guildUser.Guild.Id,
                        channel.Id,
                        firstClanId,
                        secondClanId);

            await command.FollowupAsync(
                $"✅ Seguimiento iniciado.\n" +
                $"**{firstClanName}** vs " +
                $"**{secondClanName}**\n" +
                $"Canal: {channel.Mention}\n" +
                "El panel aparecerá en un máximo " +
                "de 30 segundos.",
                ephemeral: true);
        }
        catch (ArgumentException exception)
        {
            await command.FollowupAsync(
                exception.Message,
                ephemeral: true);
        }
    }

    private async Task HandleStopComparisonAsync(
    SocketSlashCommand command)
    {
        if (command.User is not SocketGuildUser
            guildUser)
        {
            await command.RespondAsync(
                "Este comando solamente funciona " +
                "dentro de un servidor.",
                ephemeral: true);

            return;
        }

        if (!guildUser.GuildPermissions
            .ManageGuild)
        {
            await command.RespondAsync(
                "Necesitás el permiso Administrar " +
                "servidor para detener seguimientos.",
                ephemeral: true);

            return;
        }

        if (command.Channel
            is not SocketTextChannel channel)
        {
            await command.RespondAsync(
                "Este comando debe ejecutarse en " +
                "el canal del seguimiento.",
                ephemeral: true);

            return;
        }

        bool stopped =
            await _panelService
                .StopComparisonAsync(
                    guildUser.Guild.Id,
                    channel.Id);

        if (!stopped)
        {
            await command.RespondAsync(
                "No hay un seguimiento activo " +
                "en este canal.",
                ephemeral: true);

            return;
        }

        await command.RespondAsync(
            "✅ El seguimiento de clanes fue " +
            "detenido.",
            ephemeral: true);
    }

    private static string CreateProgressBar(
    decimal percentage)
    {
        const int totalBlocks = 10;

        int completedBlocks =
            (int)Math.Round(
                percentage / 100m *
                totalBlocks);

        completedBlocks = Math.Clamp(
            completedBlocks,
            0,
            totalBlocks);

        string completed =
            new('█', completedBlocks);

        string pending =
            new('░',
                totalBlocks - completedBlocks);

        return completed + pending;
    }
}
