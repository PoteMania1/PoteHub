namespace PoteHub.Api.Clients;

public class NinjaSagaApiClient
{
    private readonly HttpClient _httpClient;

    private const string ClanRankingUrl =
        "https://static.ninjasaga.cc/data/clan_rankings.json";

    public NinjaSagaApiClient()
    {
        _httpClient = new HttpClient();
    }
}