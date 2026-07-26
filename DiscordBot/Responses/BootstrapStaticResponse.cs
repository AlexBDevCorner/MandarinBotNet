using System.Text.Json.Serialization;

namespace DiscordBot.Responses;

public sealed class BootstrapStaticResponse
{
    [JsonPropertyName("events")]
    public List<PremierLeagueEvent> Events { get; init; } = null!;
}

public sealed class PremierLeagueEvent
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("is_next")]
    public bool IsNext { get; init; }

    [JsonPropertyName("deadline_time_epoch")]
    public long DeadlineTimeEpoch { get; init; }
}
