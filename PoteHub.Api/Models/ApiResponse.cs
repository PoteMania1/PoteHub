using System.Text.Json.Serialization;

namespace PoteHub.Api.Models;

public class ApiResponse
{
    [JsonPropertyName("generated_at")]
    public string GeneratedAt { get; set; } = string.Empty;

    [JsonPropertyName("season")]
    public SeasonResponse Season { get; set; } = new();

    [JsonPropertyName("clans")]
    public List<ClanResponse> Clans { get; set; } = [];
}