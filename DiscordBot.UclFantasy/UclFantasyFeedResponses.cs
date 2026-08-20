using System.Text.Json.Serialization;

namespace DiscordBot.UclFantasy;

public sealed class UclFantasyWebConfigurationResponse
{
    [JsonPropertyName("data")]
    public UclFantasyWebConfigurationData? Data { get; init; }

    [JsonPropertyName("meta")]
    public UclFantasyFeedMeta? Meta { get; init; }
}

public sealed class UclFantasyWebConfigurationData
{
    [JsonPropertyName("value")]
    public UclFantasyWebConfigurationValue? Value { get; init; }
}

public sealed class UclFantasyWebConfigurationValue
{
    [JsonPropertyName("tourId")]
    public int TourId { get; init; }

    [JsonPropertyName("FEED_BASE_URL")]
    public string? FeedBaseUrl { get; init; }

    [JsonPropertyName("fixturesUrl")]
    public string? FixturesUrl { get; init; }

    [JsonPropertyName("gameId")]
    public string? GameId { get; init; }

    [JsonPropertyName("ENVIRONMENT")]
    public string? Environment { get; init; }

    [JsonPropertyName("OffSet")]
    public string? Offset { get; init; }
}

public sealed class UclFantasyFixturesResponse
{
    [JsonPropertyName("data")]
    public UclFantasyFixturesData? Data { get; init; }

    [JsonPropertyName("meta")]
    public UclFantasyFeedMeta? Meta { get; init; }
}

public sealed class UclFantasyFixturesData
{
    [JsonPropertyName("value")]
    public IReadOnlyList<UclFantasyMatchday>? Value { get; init; }

    [JsonPropertyName("feedTime")]
    public UclFantasyFeedTime? FeedTime { get; init; }
}

public sealed class UclFantasyMatchday
{
    [JsonPropertyName("mdId")]
    public int MatchdayId { get; init; }

    [JsonPropertyName("mdIsLocked")]
    public int IsLocked { get; init; }

    [JsonPropertyName("mdIsCurrent")]
    public int IsCurrent { get; init; }

    [JsonPropertyName("deadline")]
    public string? Deadline { get; init; }

    [JsonPropertyName("gameday")]
    public int? Gameday { get; init; }

    [JsonPropertyName("gamedayNew")]
    public int? GamedayNew { get; init; }

    [JsonPropertyName("roundId")]
    public int? RoundId { get; init; }

    [JsonPropertyName("round")]
    public string? Round { get; init; }

    [JsonPropertyName("mdName")]
    public string? Name { get; init; }

    [JsonPropertyName("match")]
    public IReadOnlyList<UclFantasyMatch>? Matches { get; init; }
}

public sealed class UclFantasyMatch
{
    [JsonPropertyName("mId")]
    public int MatchId { get; init; }

    [JsonPropertyName("dateTime")]
    public string? DateTime { get; init; }

    [JsonPropertyName("dateTimeLock")]
    public string? DateTimeLock { get; init; }

    [JsonPropertyName("gmIsCurrent")]
    public int IsCurrent { get; init; }

    [JsonPropertyName("gmIsLocked")]
    public int IsLocked { get; init; }

    [JsonPropertyName("matchStatus")]
    public string? MatchStatus { get; init; }

    [JsonPropertyName("htName")]
    public string? HomeTeamName { get; init; }

    [JsonPropertyName("atName")]
    public string? AwayTeamName { get; init; }

    [JsonPropertyName("htShortName")]
    public string? HomeTeamShortName { get; init; }

    [JsonPropertyName("atShortName")]
    public string? AwayTeamShortName { get; init; }

    [JsonPropertyName("htScore")]
    public string? HomeScore { get; init; }

    [JsonPropertyName("atScore")]
    public string? AwayScore { get; init; }

    [JsonPropertyName("venueCountryCode")]
    public string? VenueCountryCode { get; init; }

    [JsonPropertyName("stadiumName")]
    public string? StadiumName { get; init; }
}

public sealed class UclFantasyFeedMeta
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("retVal")]
    public int? ReturnValue { get; init; }

    [JsonPropertyName("success")]
    public bool? Success { get; init; }
}

public sealed class UclFantasyFeedTime
{
    [JsonPropertyName("utcTime")]
    public string? UtcTime { get; init; }

    [JsonPropertyName("cestTime")]
    public string? CentralEuropeanSummerTime { get; init; }
}
