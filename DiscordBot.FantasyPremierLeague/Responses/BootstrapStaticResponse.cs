using System.Text.Json.Serialization;

namespace DiscordBot.Responses;

public sealed class BootstrapStaticResponse
{
    [JsonPropertyName("events")]
    public List<PremierLeagueEvent> Events { get; init; } = null!;

    [JsonPropertyName("elements")]
    public List<PremierLeagueElement> Elements { get; init; } = [];

}

public sealed class PremierLeagueEvent
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("is_next")]
    public bool IsNext { get; init; }

    [JsonPropertyName("finished")]
    public bool IsFinished { get; init; }

    [JsonPropertyName("is_current")]
    public bool IsCurrent { get; init; }

    [JsonPropertyName("deadline_time_epoch")]
    public long DeadlineTimeEpoch { get; init; }
}

public sealed class PremierLeagueElement
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("team")]
    public int TeamId { get; init; }

    [JsonPropertyName("element_type")]
    public int ElementType { get; init; }

    [JsonPropertyName("web_name")]
    public string WebName { get; init; } = string.Empty;

    [JsonPropertyName("now_cost")]
    public int NowCost { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("chance_of_playing_next_round")]
    public int? ChanceOfPlayingNextRound { get; init; }

    [JsonPropertyName("news")]
    public string News { get; init; } = string.Empty;
}
