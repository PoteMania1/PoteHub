using System.Text.Json.Serialization;

namespace PoteHub.Api.Models;

public class MemberResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("reputation")]
    public int Reputation { get; set; }
}