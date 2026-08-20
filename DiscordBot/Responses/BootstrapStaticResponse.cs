using System.Text.Json.Serialization;

namespace DiscordBot.Responses;

public sealed class BootstrapStaticResponse
{
    [JsonPropertyName("events")]
    public List<PremierLeagueEvent> Events { get; init; } = null!;

    [JsonPropertyName("elements")]
    public List<PremierLeagueElement> Elements { get; init; } = [];

    [JsonPropertyName("season_name")]
    public string? SeasonName { get; init; }
}

public sealed class PremierLeagueEvent
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("is_next")]
    public bool IsNext { get; init; }

    [JsonPropertyName("is_finished")]
    public bool IsFinished { get; init; }

    [JsonPropertyName("deadline_time_epoch")]
    public long DeadlineTimeEpoch { get; init; }
}

public sealed class PremierLeagueElement
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("web_name")]
    public string WebName { get; init; } = string.Empty;
}
