using System.Text.Json.Serialization;

namespace DiscordBot.Responses;

public sealed class EntryEventPicksResponse
{
    [JsonPropertyName("picks")]
    public List<EntryEventPick> Picks { get; init; } = null!;
}

public sealed class EntryEventPick
{
    [JsonPropertyName("element")]
    public int Element { get; init; }

    [JsonPropertyName("position")]
    public int Position { get; init; }

    [JsonPropertyName("multiplier")]
    public int Multiplier { get; init; }

    [JsonPropertyName("is_captain")]
    public bool IsCaptain { get; init; }

    [JsonPropertyName("is_vice_captain")]
    public bool IsViceCaptain { get; init; }
}
