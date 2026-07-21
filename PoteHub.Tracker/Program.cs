using Microsoft.Data.Sqlite;
using PoteHub.Api.Clients;
using PoteHub.Api.Mappers;
using PoteHub.Api.Models;
using PoteHub.Database.Data;
using PoteHub.Database.Initialization;
using PoteHub.Database.RepositoryBase;
using PoteHub.Domain.Entities;
using System.Text;
using System.Globalization;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("Descargando información...");

NinjaSagaApiClient apiClient = new();

ApiResponse response = await apiClient.GetClanRankingsAsync();

string dataDirectory = Path.Combine(
    Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData),
    "PoteHub");

string databasePath = Path.Combine(
    dataDirectory,
    "potehub.db");

DatabaseConnection database = new(databasePath);

Console.WriteLine($"Base de datos: {databasePath}");

DatabaseInitializer initializer = new(database);
await initializer.InitializeAsync();

SeasonRepository seasonRepository = new(database);
ClanRepository clanRepository = new(database);
MemberRepository memberRepository = new(database);
ClanSeasonRepository clanSeasonRepository = new(database);
MemberParticipationRepository participationRepository = new(database);
SyncRunRepository syncRunRepository = new(database);
ClanChangeRepository clanChangeRepository = new(database);
MemberChangeRepository memberChangeRepository = new(database);
MemberMovementRepository movementRepository = new(database);

Season season = SeasonMapper.ToDomain(response.Season);

DateTime generatedAt = DateTime.SpecifyKind(
    DateTime.ParseExact(
        response.GeneratedAt,
        "yyyy-MM-dd HH:mm:ss",
        CultureInfo.InvariantCulture),
    DateTimeKind.Utc);

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
        "La API devolvió miembros duplicados dentro " +
        $"del mismo clan: {examples}");
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
                $"en clanes {string.Join("/", item.Clans)}"));

    throw new InvalidDataException(
        "La API devolvió el mismo MemberId en varios " +
        $"clanes: {examples}");
}

int newClans = 0;
int changedClans = 0;
int newParticipations = 0;
int changedParticipations = 0;
int enteredMembers = 0;
int changedClanMembers = 0;
int missingMembers = 0;

using SqliteConnection connection = database.CreateConnection();

await connection.OpenAsync();

using SqliteTransaction transaction = connection.BeginTransaction();

try
{
    await seasonRepository.SaveAsync(
        season,
        connection,
        transaction);

    bool isInitialSync =
    !await syncRunRepository.HasAnyBySeasonAsync(
        season.SeasonId,
        connection,
        transaction);

    bool alreadyProcessed =
        await syncRunRepository.ExistsByGeneratedAtAsync(
            season.SeasonId,
            generatedAt,
            connection,
            transaction);

    if (alreadyProcessed)
    {
        await transaction.RollbackAsync();

        Console.WriteLine(
            $"La respuesta {response.GeneratedAt} " +
            "ya había sido procesada.");

        return;
    }

    DateTime syncDate = DateTime.UtcNow;

    SyncRun syncRun = new()
    {
        SeasonId = season.SeasonId,
        ExecutedAt = syncDate,
        GeneratedAt = generatedAt
    };

    long syncRunId = await syncRunRepository.CreateAsync(
        syncRun,
        connection,
        transaction);

    List<MemberParticipation> activeParticipations =
    await participationRepository.GetActiveBySeasonAsync(
        season.SeasonId,
        connection,
        transaction);


    Dictionary<int, List<MemberParticipation>> activeByMember =
        activeParticipations
            .GroupBy(p => p.MemberId)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

    HashSet<(int MemberId, int ClanId)> seenParticipations = [];

    foreach (ClanResponse clanResponse in response.Clans)
    {
        Clan clan = ClanMapper.ToDomain(clanResponse);

        await clanRepository.SaveAsync(
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
            await clanSeasonRepository.GetAsync(
                season.SeasonId,
                clan.ClanId,
                connection,
                transaction);

        if (previousClan is null)
        {
            newClans++;

            Console.WriteLine($"Nuevo clan: {clan.Name}");
        }
        else
        {
            int reputationDifference =
                clanSeason.Reputation - previousClan.Reputation;

            int rankDifference =
                clanSeason.Rank - previousClan.Rank;

            int memberDifference =
                clanSeason.MemberCount - previousClan.MemberCount;

            int deductionDifference =
                clanSeason.Deduction - previousClan.Deduction;

            bool clanChanged =
                reputationDifference != 0 ||
                rankDifference != 0 ||
                memberDifference != 0 ||
                deductionDifference != 0;

            if (clanChanged)
            {
                changedClans++;

                ClanChange change = new()
                {
                    SyncRunId = syncRunId,
                    SeasonId = season.SeasonId,
                    ClanId = clan.ClanId,

                    PreviousRank = previousClan.Rank,
                    CurrentRank = clanSeason.Rank,

                    PreviousMemberCount = previousClan.MemberCount,
                    CurrentMemberCount = clanSeason.MemberCount,

                    PreviousReputation = previousClan.Reputation,
                    CurrentReputation = clanSeason.Reputation,
                    ReputationDifference = reputationDifference,

                    PreviousDeduction = previousClan.Deduction,
                    CurrentDeduction = clanSeason.Deduction,

                    DetectedAt = syncDate
                };

                await clanChangeRepository.SaveAsync(
                    change,
                    connection,
                    transaction);

                List<string> changes = [];

                if (reputationDifference != 0)
                {
                    changes.Add(
                        $"reputación {reputationDifference:+#;-#;0}");
                }

                if (rankDifference != 0)
                {
                    changes.Add(
                        $"puesto {previousClan.Rank} → {clanSeason.Rank}");
                }

                if (memberDifference != 0)
                {
                    changes.Add(
                        $"miembros {memberDifference:+#;-#;0}");
                }

                if (deductionDifference != 0)
                {
                    changes.Add(
                        $"deducción {deductionDifference:+#;-#;0}");
                }

                Console.WriteLine(
                    $"Clan {clan.Name}: {string.Join(", ", changes)}");
            }
        }

        await clanSeasonRepository.SaveAsync(
            clanSeason,
            connection,
            transaction);

        foreach (MemberResponse memberResponse in clanResponse.MemberList)
        {
            Member member = MemberMapper.ToDomain(memberResponse);

            await memberRepository.SaveAsync(
                member,
                connection,
                transaction);

            seenParticipations.Add(
                (member.MemberId, clan.ClanId));

            activeByMember.TryGetValue(
                member.MemberId,
                out List<MemberParticipation>? previousActiveList);

            previousActiveList ??= [];

            MemberParticipation? sameClanParticipation =
                previousActiveList.FirstOrDefault(
                    participation =>
                        participation.ClanId == clan.ClanId &&
                        participation.IsActive);

            if (sameClanParticipation is null)
            {
                MemberParticipation? previousClanParticipation =
                    previousActiveList.FirstOrDefault(
                        participation => participation.IsActive);

                if (previousClanParticipation is null)
                {
                    newParticipations++;

                    if (!isInitialSync)
                    {
                        enteredMembers++;

                        MemberMovement enteredMovement = new()
                        {
                            SyncRunId = syncRunId,
                            SeasonId = season.SeasonId,
                            MemberId = member.MemberId,
                            FromClanId = null,
                            ToClanId = clan.ClanId,
                            MovementType = "EnteredClan",
                            DetectedAt = syncDate
                        };

                        await movementRepository.SaveAsync(
                            enteredMovement,
                            connection,
                            transaction);

                        Console.WriteLine(
                            $"Entrada: {member.Name} → {clan.Name}");
                    }
                }
                else
                {
                    changedClanMembers++;

                    Clan? previousClanEntity =
                    await clanRepository.GetByIdAsync(
                    previousClanParticipation.ClanId,
                    connection,
                    transaction);

                    string previousClanName =
                        previousClanEntity?.Name ??
                        $"Clan {previousClanParticipation.ClanId}";

                    foreach (MemberParticipation previousParticipation
                             in previousActiveList.Where(p => p.IsActive))
                    {
                        await participationRepository.DeactivateAsync(
                            season.SeasonId,
                            member.MemberId,
                            previousParticipation.ClanId,
                            connection,
                            transaction);

                        previousParticipation.IsActive = false;
                    }

                    MemberMovement movement = new()
                    {
                        SyncRunId = syncRunId,
                        SeasonId = season.SeasonId,
                        MemberId = member.MemberId,
                        FromClanId = previousClanParticipation.ClanId,
                        ToClanId = clan.ClanId,
                        MovementType = "ChangedClan",
                        DetectedAt = syncDate
                    };

                    await movementRepository.SaveAsync(
                        movement,
                        connection,
                        transaction);

                    Console.WriteLine(
                        $"Cambio de clan: {member.Name} " +
                        $"{previousClanName} → {clan.Name}");
                }
            }
            else if (
                sameClanParticipation.Reputation !=
                memberResponse.Reputation)
            {
                changedParticipations++;

                int reputationDifference =
                    memberResponse.Reputation -
                    sameClanParticipation.Reputation;

                MemberChange change = new()
                {
                    SyncRunId = syncRunId,
                    SeasonId = season.SeasonId,
                    MemberId = member.MemberId,
                    ClanId = clan.ClanId,
                    PreviousReputation =
                        sameClanParticipation.Reputation,
                    CurrentReputation =
                        memberResponse.Reputation,
                    ReputationDifference =
                        reputationDifference,
                    DetectedAt = syncDate
                };

                await memberChangeRepository.SaveAsync(
                    change,
                    connection,
                    transaction);

                Console.WriteLine(
                    $"Miembro {member.Name}: reputación " +
                    $"{reputationDifference:+#;-#;0}");
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

            await participationRepository.SaveAsync(
                currentParticipation,
                connection,
                transaction);
        }

        /*foreach (MemberParticipation previousParticipation
             in activeParticipations)
        {
            bool wasSeen = seenParticipations.Contains(
                (
                    previousParticipation.MemberId,
                    previousParticipation.ClanId
                ));

            if (wasSeen || !previousParticipation.IsActive)
            {
                continue;
            }

            await participationRepository.DeactivateAsync(
                previousParticipation.SeasonId,
                previousParticipation.MemberId,
                previousParticipation.ClanId,
                connection,
                transaction);

            MemberMovement missingMovement = new()
            {
                SyncRunId = syncRunId,
                SeasonId = season.SeasonId,
                MemberId = previousParticipation.MemberId,
                FromClanId = previousParticipation.ClanId,
                ToClanId = null,
                MovementType = "MissingFromApi",
                DetectedAt = syncDate
            };

            await movementRepository.SaveAsync(
                missingMovement,
                connection,
                transaction);

            missingMembers++;

            Console.WriteLine(
                $"Ya no aparece en la API: miembro " +
                $"{previousParticipation.MemberId}");
        }*/
    }

    await syncRunRepository.UpdateTotalsAsync(
    syncRunId,
    changedClans,
    changedParticipations,
    enteredMembers,
    changedClanMembers,
    missingMembers,
    connection,
    transaction);

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

Console.WriteLine();
Console.WriteLine("Sincronización finalizada.");
Console.WriteLine($"Clanes nuevos: {newClans}");
Console.WriteLine($"Clanes modificados: {changedClans}");
Console.WriteLine($"Participaciones nuevas: {newParticipations}");
Console.WriteLine($"Participaciones modificadas: {changedParticipations}");
Console.WriteLine($"Entradas a clanes: {enteredMembers}");
Console.WriteLine($"Cambios de clan: {changedClanMembers}");
Console.WriteLine($"Ausentes en la API: {missingMembers}");