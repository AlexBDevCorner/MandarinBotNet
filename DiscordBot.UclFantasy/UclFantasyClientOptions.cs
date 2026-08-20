namespace DiscordBot.UclFantasy;

public sealed class UclFantasyClientOptions
{
    public Uri BaseAddress { get; init; } =
        new("https://gaming.uefa.com/en/uclfantasy/");

    public string WebConfigurationPath { get; init; } =
        "services/feeds/config/web.json";

    public string FeedBasePath { get; init; } = "services/feeds/";

    public string FixturesPathTemplate { get; init; } =
        "fixtures/fixtures_{{tour_id}}_{{lang}}.json";

    public string Language { get; init; } = "en";

    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan TotalRequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxRetryAttempts { get; init; } = 2;

    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    public bool UseRetryJitter { get; init; } = true;

    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    public int CircuitBreakerMinimumThroughput { get; init; } = 10;

    public TimeSpan CircuitBreakerSamplingDuration { get; init; } =
        TimeSpan.FromSeconds(30);

    public TimeSpan CircuitBreakerBreakDuration { get; init; } =
        TimeSpan.FromSeconds(30);
}
