using System.Text.Json.Serialization;

namespace DiscordBot.Responses;

public sealed class EventLiveResponse
{
    [JsonPropertyName("elements")]
    public List<EventLiveElement> Elements { get; init; } = null!;
}

public sealed class EventLiveElement
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("stats")]
    public EventLiveElementStats Stats { get; init; } = null!;
}

public sealed class EventLiveElementStats
{
    [JsonPropertyName("total_points")]
    [JsonRequired]
    public int TotalPoints { get; init; }

    [JsonPropertyName("minutes")]
    public int Minutes { get; init; }
}
