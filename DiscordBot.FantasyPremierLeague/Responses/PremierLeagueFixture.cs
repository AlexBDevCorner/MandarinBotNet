using System.Text.Json.Serialization;

namespace DiscordBot.Responses;

public sealed class PremierLeagueFixture
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("event")]
    public int EventId { get; init; }

    [JsonPropertyName("team_h")]
    public int HomeTeamId { get; init; }

    [JsonPropertyName("team_a")]
    public int AwayTeamId { get; init; }

    [JsonPropertyName("kickoff_time")]
    public DateTimeOffset? KickoffTimeUtc { get; init; }

    [JsonPropertyName("started")]
    public bool? Started { get; init; }

    [JsonPropertyName("finished")]
    public bool? Finished { get; init; }

    [JsonPropertyName("team_h_difficulty")]
    public int HomeTeamDifficulty { get; init; }

    [JsonPropertyName("team_a_difficulty")]
    public int AwayTeamDifficulty { get; init; }
}
