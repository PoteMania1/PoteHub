using Microsoft.Data.Sqlite;
using PoteHub.Api.Clients;
using PoteHub.Api.Mappers;
using PoteHub.Api.Models;
using PoteHub.Database.Data;
using PoteHub.Database.Initialization;
using PoteHub.Database.RepositoryBase;
using PoteHub.Domain.Entities;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("Descargando información...");

NinjaSagaApiClient apiClient = new();

ApiResponse response = await apiClient.GetClanRankingsAsync();

DatabaseConnection database = new("potehub.db");

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

Season season = SeasonMapper.ToDomain(response.Season);

int newClans = 0;
int changedClans = 0;
int newParticipations = 0;
int changedParticipations = 0;

using SqliteConnection connection = database.CreateConnection();

await connection.OpenAsync();

using SqliteTransaction transaction = connection.BeginTransaction();

try
{
    await seasonRepository.SaveAsync(
        season,
        connection,
        transaction);

    DateTime syncDate = DateTime.UtcNow;

    SyncRun syncRun = new()
    {
        SeasonId = season.SeasonId,
        ExecutedAt = syncDate
    };

    long syncRunId = await syncRunRepository.CreateAsync(
        syncRun,
        connection,
        transaction);

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

            MemberParticipation participation = new()
            {
                SeasonId = season.SeasonId,
                MemberId = member.MemberId,
                ClanId = clan.ClanId,
                Reputation = memberResponse.Reputation
            };

            MemberParticipation? previousParticipation =
                await participationRepository.GetAsync(
                    season.SeasonId,
                    member.MemberId,
                    clan.ClanId,
                    connection,
                    transaction);

            if (previousParticipation is null)
            {
                newParticipations++;

                Console.WriteLine(
                    $"Nueva participación: {member.Name} → {clan.Name}");
            }
            else if (
                previousParticipation.Reputation !=
                participation.Reputation)
            {
                changedParticipations++;

                int reputationDifference =
                    participation.Reputation -
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
                        participation.Reputation,
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

            await participationRepository.SaveAsync(
                participation,
                connection,
                transaction);
        }
    }

    await syncRunRepository.UpdateTotalsAsync(
        syncRunId,
        changedClans,
        changedParticipations,
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