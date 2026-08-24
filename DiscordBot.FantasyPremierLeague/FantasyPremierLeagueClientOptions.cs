namespace DiscordBot.FantasyPremierLeague;

public sealed class FantasyPremierLeagueClientOptions
{
    public Uri BaseAddress { get; init; } = new("https://fantasy.premierleague.com");

    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan TotalRequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxRetryAttempts { get; init; } = 2;

    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    public bool UseRetryJitter { get; init; } = true;

    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    public int CircuitBreakerMinimumThroughput { get; init; } = 10;

    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(30);
}
