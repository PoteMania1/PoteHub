using System.Text.Json;
using PoteHub.Api.Models;

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

    public async Task<string> GetClanRankingsJsonAsync()
    {
        string json = await _httpClient.GetStringAsync(ClanRankingUrl);

        return json;
    }

    public async Task<ApiResponse> GetClanRankingsAsync()
    {
        string json = await GetClanRankingsJsonAsync();

        ApiResponse? response =
            JsonSerializer.Deserialize<ApiResponse>(json);

        if (response is null)
        {
            throw new InvalidOperationException(
                "No se pudo convertir la respuesta de la API.");
        }

        return response;
    }
}