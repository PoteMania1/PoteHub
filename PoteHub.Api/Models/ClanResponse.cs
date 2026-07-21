using System.Text.Json.Serialization;

namespace PoteHub.Api.Models;

public class ClanResponse
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("master")]
    public string Master { get; set; } = string.Empty;

    [JsonPropertyName("members")]
    public int Members { get; set; }

    [JsonPropertyName("reputation")]
    public int Reputation { get; set; }

    [JsonPropertyName("deduction")]
    public int Deduction { get; set; }

    [JsonPropertyName("member_list")]
    public List<MemberResponse> MemberList { get; set; } = [];
}