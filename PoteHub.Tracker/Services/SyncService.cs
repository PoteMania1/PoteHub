using Microsoft.Data.Sqlite;
using PoteHub.Api.Clients;
using PoteHub.Api.Mappers;
using PoteHub.Api.Models;
using PoteHub.Database.Data;
using PoteHub.Database.Initialization;
using PoteHub.Database.RepositoryBase;
using PoteHub.Domain.Entities;
using PoteHub.Tracker.Models;
using System.Globalization;

namespace PoteHub.Tracker.Services;

public class SyncService
{
    private readonly NinjaSagaApiClient _apiClient;
    private readonly DatabaseConnection _database;

    private readonly DatabaseInitializer _initializer;
    private readonly SeasonRepository _seasonRepository;
    private readonly ClanRepository _clanRepository;
    private readonly MemberRepository _memberRepository;
    private readonly ClanSeasonRepository _clanSeasonRepository;
    private readonly CalendarRepository _calendarRepository;

    private readonly SeasonCalendarService
        _calendarService;

    private readonly MemberParticipationRepository
        _participationRepository;

    private readonly SyncRunRepository _syncRunRepository;
    private readonly ClanChangeRepository _clanChangeRepository;
    private readonly MemberChangeRepository _memberChangeRepository;

    private readonly MemberMovementRepository
        _movementRepository;

    private readonly DataRetentionRepository
        _retentionRepository;

    public SyncService(
        NinjaSagaApiClient apiClient,
        DatabaseConnection database)
    {
        _apiClient = apiClient;
        _database = database;
        _calendarRepository =
        new CalendarRepository(database);

        _calendarService =
            new SeasonCalendarService(
                _calendarRepository);

        _initializer = new DatabaseInitializer(database);
        _seasonRepository = new SeasonRepository(database);
        _clanRepository = new ClanRepository(database);
        _memberRepository = new MemberRepository(database);

        _clanSeasonRepository =
            new ClanSeasonRepository(database);

        _participationRepository =
            new MemberParticipationRepository(database);

        _syncRunRepository =
            new SyncRunRepository(database);

        _clanChangeRepository =
            new ClanChangeRepository(database);

        _memberChangeRepository =
            new MemberChangeRepository(database);

        _movementRepository =
            new MemberMovementRepository(database);

        _retentionRepository =
            new DataRetentionRepository(database);
    }

    private async Task SynchronizeClanAsync(
    ClanResponse clanResponse,
    Season season,
    long syncRunId,
    DateTime syncDate,
    DateTime generatedAt,
    bool isInitialSync,
    Dictionary<int, List<MemberParticipation>>
        activeByMember,
    SyncResult result,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        Clan clan = ClanMapper.ToDomain(clanResponse);

        await _clanRepository.SaveAsync(
            clan,
            connection,
            transaction);

        ClanSeason clanSeason = new()
        {
            SeasonId = season.SeasonId,
            ClanId = clan.ClanId,
            Rank = clanResponse.Rank,
            MemberCount = clanResponse.Members,
            Reputation = clanResponse.Reputation,
            Deduction = clanResponse.Deduction
        };

        ClanSeason? previousClan =
            await _clanSeasonRepository.GetAsync(
                season.SeasonId,
                clan.ClanId,
                connection,
                transaction);

        if (previousClan is null)
        {
            result.NewClans++;

            result.Messages.Add(
                $"Nuevo clan: {clan.Name}");
        }
        else
        {
            await RegisterClanChangeAsync(
                clan,
                previousClan,
                clanSeason,
                season.SeasonId,
                syncRunId,
                syncDate,
                result,
                connection,
                transaction);
        }

        await _clanSeasonRepository.SaveAsync(
            clanSeason,
            connection,
            transaction);

        foreach (MemberResponse memberResponse
                 in clanResponse.MemberList)
        {
            await SynchronizeMemberAsync(
                memberResponse,
                clan,
                season,
                syncRunId,
                syncDate,
                generatedAt,
                isInitialSync,
                activeByMember,
                result,
                connection,
                transaction);
        }
    }

    private async Task SynchronizeMemberAsync(
    MemberResponse memberResponse,
    Clan clan,
    Season season,
    long syncRunId,
    DateTime syncDate,
    DateTime generatedAt,
    bool isInitialSync,
    Dictionary<int, List<MemberParticipation>>
        activeByMember,
    SyncResult result,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        Member member =
            MemberMapper.ToDomain(memberResponse);

        await _memberRepository.SaveAsync(
            member,
            connection,
            transaction);

        activeByMember.TryGetValue(
            member.MemberId,
            out List<MemberParticipation>?
                previousActiveList);

        previousActiveList ??= [];

        MemberParticipation? sameClanParticipation =
            previousActiveList.FirstOrDefault(
                participation =>
                    participation.ClanId ==
                        clan.ClanId &&
                    participation.IsActive);

        if (sameClanParticipation is null)
        {
            MemberParticipation?
                previousClanParticipation =
                    previousActiveList.FirstOrDefault(
                        participation =>
                            participation.IsActive);

            if (previousClanParticipation is null)
            {
                result.NewParticipations++;

                if (!isInitialSync)
                {
                    await RegisterEntryAsync(
                        member,
                        clan,
                        season,
                        syncRunId,
                        syncDate,
                        result,
                        connection,
                        transaction);
                }
            }
            else
            {
                await RegisterClanMovementAsync(
                    member,
                    clan,
                    season,
                    previousClanParticipation,
                    previousActiveList,
                    syncRunId,
                    syncDate,
                    result,
                    connection,
                    transaction);
            }
        }
        else if (
            sameClanParticipation.Reputation !=
            memberResponse.Reputation)
        {
            await RegisterMemberChangeAsync(
                member,
                clan,
                season,
                sameClanParticipation,
                memberResponse.Reputation,
                syncRunId,
                syncDate,
                result,
                connection,
                transaction);
        }

        MemberParticipation currentParticipation = new()
        {
            SeasonId = season.SeasonId,
            MemberId = member.MemberId,
            ClanId = clan.ClanId,
            Reputation = memberResponse.Reputation,
            IsActive = true,
            LastSeenAt = syncDate
        };

        await _participationRepository.SaveAsync(
            currentParticipation,
            connection,
            transaction);

        const int requiredReputation = 10000;

        DateTime lastDayStart =
            season.EndTime.AddDays(-1);

        bool reachedRequirementInTime =
            currentParticipation.Reputation >=
                requiredReputation &&
            generatedAt < lastDayStart;

        if (reachedRequirementInTime)
        {
            await _participationRepository
                .MarkRewardQualifiedAsync(
                    season.SeasonId,
                    member.MemberId,
                    clan.ClanId,
                    generatedAt,
                    connection,
                    transaction);
        }

        if (!activeByMember.TryGetValue(
                member.MemberId,
                out List<MemberParticipation>?
                    currentActiveList))
        {
            currentActiveList = [];
            activeByMember[member.MemberId] =
                currentActiveList;
        }

        MemberParticipation? storedParticipation =
            currentActiveList.FirstOrDefault(
                participation =>
                    participation.ClanId == clan.ClanId);

        if (storedParticipation is null)
        {
            currentActiveList.Add(currentParticipation);
        }
        else
        {
            storedParticipation.Reputation =
                currentParticipation.Reputation;

            storedParticipation.IsActive = true;
            storedParticipation.LastSeenAt = syncDate;
        }
    }

    private async Task RegisterEntryAsync(
    Member member,
    Clan clan,
    Season season,
    long syncRunId,
    DateTime syncDate,
    SyncResult result,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        result.EnteredMembers++;

        MemberMovement movement = new()
        {
            SyncRunId = syncRunId,
            SeasonId = season.SeasonId,
            MemberId = member.MemberId,
            FromClanId = null,
            ToClanId = clan.ClanId,
            MovementType = "EnteredClan",
            DetectedAt = syncDate
        };

        await _movementRepository.SaveAsync(
            movement,
            connection,
            transaction);

        result.Messages.Add(
            $"Entrada: {member.Name} → {clan.Name}");
    }

    private async Task RegisterClanMovementAsync(
    Member member,
    Clan newClan,
    Season season,
    MemberParticipation previousClanParticipation,
    List<MemberParticipation> previousActiveList,
    long syncRunId,
    DateTime syncDate,
    SyncResult result,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        result.ChangedClanMembers++;

        Clan? previousClan =
            await _clanRepository.GetByIdAsync(
                previousClanParticipation.ClanId,
                connection,
                transaction);

        string previousClanName =
            previousClan?.Name ??
            $"Clan {previousClanParticipation.ClanId}";

        foreach (MemberParticipation participation
                 in previousActiveList
                     .Where(item => item.IsActive))
        {
            await _participationRepository.DeactivateAsync(
                season.SeasonId,
                member.MemberId,
                participation.ClanId,
                connection,
                transaction);

            participation.IsActive = false;
        }

        MemberMovement movement = new()
        {
            SyncRunId = syncRunId,
            SeasonId = season.SeasonId,
            MemberId = member.MemberId,

            FromClanId =
                previousClanParticipation.ClanId,

            ToClanId = newClan.ClanId,
            MovementType = "ChangedClan",
            DetectedAt = syncDate
        };

        await _movementRepository.SaveAsync(
            movement,
            connection,
            transaction);

        result.Messages.Add(
            $"Cambio de clan: {member.Name} " +
            $"{previousClanName} → {newClan.Name}");
    }

    private async Task RegisterMemberChangeAsync(
    Member member,
    Clan clan,
    Season season,
    MemberParticipation previousParticipation,
    int currentReputation,
    long syncRunId,
    DateTime syncDate,
    SyncResult result,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        result.ChangedParticipations++;

        int difference =
            currentReputation -
            previousParticipation.Reputation;

        MemberChange change = new()
        {
            SyncRunId = syncRunId,
            SeasonId = season.SeasonId,
            MemberId = member.MemberId,
            ClanId = clan.ClanId,

            PreviousReputation =
                previousParticipation.Reputation,

            CurrentReputation =
                currentReputation,

            ReputationDifference = difference,
            DetectedAt = syncDate
        };

        await _memberChangeRepository.SaveAsync(
            change,
            connection,
            transaction);

        result.Messages.Add(
            $"Miembro {member.Name}: reputación " +
            $"{difference:+#;-#;0}");
    }

    private async Task RegisterClanChangeAsync(
    Clan clan,
    ClanSeason previousClan,
    ClanSeason currentClan,
    int seasonId,
    long syncRunId,
    DateTime syncDate,
    SyncResult result,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        int reputationDifference =
            currentClan.Reputation -
            previousClan.Reputation;

        int rankDifference =
            currentClan.Rank -
            previousClan.Rank;

        int memberDifference =
            currentClan.MemberCount -
            previousClan.MemberCount;

        int deductionDifference =
            currentClan.Deduction -
            previousClan.Deduction;

        bool changed =
            reputationDifference != 0 ||
            rankDifference != 0 ||
            memberDifference != 0 ||
            deductionDifference != 0;

        if (!changed)
        {
            return;
        }

        result.ChangedClans++;

        ClanChange change = new()
        {
            SyncRunId = syncRunId,
            SeasonId = seasonId,
            ClanId = clan.ClanId,

            PreviousRank = previousClan.Rank,
            CurrentRank = currentClan.Rank,

            PreviousMemberCount =
                previousClan.MemberCount,

            CurrentMemberCount =
                currentClan.MemberCount,

            PreviousReputation =
                previousClan.Reputation,

            CurrentReputation =
                currentClan.Reputation,

            ReputationDifference =
                reputationDifference,

            PreviousDeduction =
                previousClan.Deduction,

            CurrentDeduction =
                currentClan.Deduction,

            DetectedAt = syncDate
        };

        await _clanChangeRepository.SaveAsync(
            change,
            connection,
            transaction);

        List<string> changes = [];

        if (reputationDifference != 0)
        {
            changes.Add(
                $"reputación " +
                $"{reputationDifference:+#;-#;0}");
        }

        if (rankDifference != 0)
        {
            changes.Add(
                $"puesto {previousClan.Rank} " +
                $"→ {currentClan.Rank}");
        }

        if (memberDifference != 0)
        {
            changes.Add(
                $"miembros {memberDifference:+#;-#;0}");
        }

        if (deductionDifference != 0)
        {
            changes.Add(
                $"deducción " +
                $"{deductionDifference:+#;-#;0}");
        }

        result.Messages.Add(
            $"Clan {clan.Name}: " +
            string.Join(", ", changes));
    }
    public async Task<SyncResult> RunAsync(
    CancellationToken cancellationToken =
        default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        await _initializer.InitializeAsync();

        ApiResponse response =
            await _apiClient.GetClanRankingsAsync(
                cancellationToken);

        cancellationToken
            .ThrowIfCancellationRequested();

        ValidateResponse(response);

        Season season =
            SeasonMapper.ToDomain(response.Season);

        DateTime generatedAt =
            ParseServerDate(response.GeneratedAt);

        SyncResult result = new()
        {
            GeneratedAt = response.GeneratedAt
        };

        using SqliteConnection connection =
            _database.CreateConnection();

        await connection.OpenAsync();

        using SqliteTransaction transaction =
            connection.BeginTransaction();

        try
        {
            await SynchronizeAsync(
                response,
                season,
                generatedAt,
                result,
                connection,
                transaction);

            if (result.AlreadyProcessed)
            {
                await transaction.RollbackAsync();
                return result;
            }

            await transaction.CommitAsync();

            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task SynchronizeAsync(
    ApiResponse response,
    Season season,
    DateTime generatedAt,
    SyncResult result,
    SqliteConnection connection,
    SqliteTransaction transaction)
    {
        await _seasonRepository.SaveAsync(
            season,
            connection,
            transaction);

        bool isInitialSync =
            !await _syncRunRepository.HasAnyBySeasonAsync(
                season.SeasonId,
                connection,
                transaction);

        bool alreadyProcessed =
            await _syncRunRepository.ExistsByGeneratedAtAsync(
                season.SeasonId,
                generatedAt,
                connection,
                transaction);

        if (alreadyProcessed)
        {
            result.AlreadyProcessed = true;
            return;
        }

        DateTime syncDate = DateTime.UtcNow;

        await _calendarService.EnsureCalendarAsync(
            season,
            connection,
            transaction);

        (Day currentDay, Wave currentWave) =
            await _calendarService.GetCurrentWaveAsync(
                season,
                generatedAt,
                connection,
                transaction);

        await _calendarRepository.FinalizePastWavesAsync(
    season.SeasonId,
    generatedAt,
    connection,
    transaction);

        RetentionResult retention =
            await _retentionRepository.RunAsync(
                season.SeasonId,
                currentDay.DayId,
                currentDay.DayNumber,
                generatedAt,
                connection,
                transaction);

        if (retention.DailyMaintenanceRan)
        {
            result.Messages.Add(
                "Mantenimiento diario ejecutado. " +
                $"Días resumidos: " +
                $"{retention.SummarizedDays}. " +
                $"Cambios de clanes eliminados: " +
                $"{retention.DeletedClanChanges}. " +
                $"Cambios de miembros eliminados: " +
                $"{retention.DeletedMemberChanges}. " +
                $"Sincronizaciones eliminadas: " +
                $"{retention.DeletedSyncRuns}.");
        }

        if (retention.FinalizedSeasons > 0)
        {
            result.Messages.Add(
                $"Temporadas finalizadas y compactadas: " +
                $"{retention.FinalizedSeasons}.");
        }

        SyncRun syncRun = new()
        {
            SeasonId = season.SeasonId,
            DayId = currentDay.DayId,
            WaveId = currentWave.WaveId,
            ExecutedAt = syncDate,
            GeneratedAt = generatedAt
        };

        long syncRunId =
            await _syncRunRepository.CreateAsync(
                syncRun,
                connection,
                transaction);

        await _calendarRepository
        .RegisterSuccessfulSyncAsync(
        currentWave.WaveId,
        generatedAt,
        connection,
        transaction);

        List<MemberParticipation> activeParticipations =
            await _participationRepository
                .GetActiveBySeasonAsync(
                    season.SeasonId,
                    connection,
                    transaction);

        Dictionary<int, List<MemberParticipation>>
            activeByMember = activeParticipations
                .GroupBy(participation =>
                    participation.MemberId)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList());

        foreach (ClanResponse clanResponse in response.Clans)
        {
            await SynchronizeClanAsync(
                clanResponse,
                season,
                syncRunId,
                syncDate,
                generatedAt,
                isInitialSync,
                activeByMember,
                result,
                connection,
                transaction);
        }

        await _syncRunRepository.UpdateTotalsAsync(
            syncRunId,
            result.ChangedClans,
            result.ChangedParticipations,
            result.EnteredMembers,
            result.ChangedClanMembers,
            result.MissingMembers,
            connection,
            transaction);
    }
    private static DateTime ParseServerDate(string value)
    {
        return DateTime.SpecifyKind(
            DateTime.ParseExact(
                value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture),
            DateTimeKind.Utc);
    }
    private static void ValidateResponse(
    ApiResponse response)
    {
        List<int> duplicatedClanIds = response.Clans
            .GroupBy(clan => clan.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicatedClanIds.Count > 0)
        {
            throw new InvalidDataException(
                "La API devolvió IDs de clan duplicados: " +
                string.Join(", ", duplicatedClanIds));
        }

        var apiMembers = response.Clans
            .SelectMany(
                clan => clan.MemberList.Select(
                    member => new
                    {
                        ClanId = clan.Id,
                        ClanName = clan.Name,
                        MemberId = member.Id,
                        MemberName = member.Name
                    }))
            .ToList();

        var duplicatedMemberRows = apiMembers
            .GroupBy(item => new
            {
                item.ClanId,
                item.MemberId
            })
            .Where(group => group.Count() > 1)
            .ToList();

        if (duplicatedMemberRows.Count > 0)
        {
            string examples = string.Join(
                ", ",
                duplicatedMemberRows
                    .Take(10)
                    .Select(group =>
                        $"miembro {group.Key.MemberId} " +
                        $"en clan {group.Key.ClanId}"));

            throw new InvalidDataException(
                "La API devolvió miembros duplicados " +
                $"dentro del mismo clan: {examples}");
        }

        var membersInMultipleClans = apiMembers
            .GroupBy(item => item.MemberId)
            .Select(group => new
            {
                MemberId = group.Key,
                MemberName = group.First().MemberName,

                Clans = group
                    .Select(item => item.ClanId)
                    .Distinct()
                    .ToList()
            })
            .Where(item => item.Clans.Count > 1)
            .ToList();

        if (membersInMultipleClans.Count > 0)
        {
            string examples = string.Join(
                ", ",
                membersInMultipleClans
                    .Take(10)
                    .Select(item =>
                        $"{item.MemberName} ({item.MemberId}) " +
                        $"en clanes " +
                        $"{string.Join("/", item.Clans)}"));

            throw new InvalidDataException(
                "La API devolvió el mismo MemberId " +
                $"en varios clanes: {examples}");
        }
    }

}