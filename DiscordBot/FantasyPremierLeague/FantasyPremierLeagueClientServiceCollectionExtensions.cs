using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace DiscordBot.FantasyPremierLeague;

public static class FantasyPremierLeagueClientServiceCollectionExtensions
{
    public static IHttpClientBuilder AddFantasyPremierLeagueClient(
        this IServiceCollection services,
        FantasyPremierLeagueClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        options ??= new FantasyPremierLeagueClientOptions();
        Validate(options);
        services.TryAddSingleton(new FantasyPremierLeagueOptions());

        var clientBuilder = services.AddHttpClient<
            IFantasyPremierLeagueClient,
            FantasyPremierLeagueClient>(client =>
        {
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        clientBuilder.AddStandardResilienceHandler(resilience =>
        {
            resilience.AttemptTimeout.Timeout = options.AttemptTimeout;
            resilience.TotalRequestTimeout.Timeout = options.TotalRequestTimeout;

            resilience.Retry.MaxRetryAttempts = options.MaxRetryAttempts;
            resilience.Retry.Delay = options.RetryDelay;
            resilience.Retry.BackoffType = DelayBackoffType.Exponential;
            resilience.Retry.UseJitter = options.UseRetryJitter;

            resilience.CircuitBreaker.FailureRatio =
                options.CircuitBreakerFailureRatio;
            resilience.CircuitBreaker.MinimumThroughput =
                options.CircuitBreakerMinimumThroughput;
            resilience.CircuitBreaker.SamplingDuration =
                options.CircuitBreakerSamplingDuration;
            resilience.CircuitBreaker.BreakDuration =
                options.CircuitBreakerBreakDuration;
        });

        return clientBuilder;
    }

    private static void Validate(FantasyPremierLeagueClientOptions options)
    {
        if (!options.BaseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException(
                "The Fantasy Premier League base address must be absolute.",
                nameof(options));
        }

        if (options.AttemptTimeout <= TimeSpan.Zero ||
            options.TotalRequestTimeout <= options.AttemptTimeout)
        {
            throw new ArgumentException(
                "The total request timeout must be greater than the positive attempt timeout.",
                nameof(options));
        }

        if (options.MaxRetryAttempts < 1 || options.RetryDelay < TimeSpan.Zero)
        {
            throw new ArgumentException(
                "At least one retry attempt is required and the delay cannot be negative.",
                nameof(options));
        }

        if (options.CircuitBreakerFailureRatio is <= 0 or > 1 ||
            options.CircuitBreakerMinimumThroughput <= 0 ||
            options.CircuitBreakerSamplingDuration < options.AttemptTimeout * 2 ||
            options.CircuitBreakerBreakDuration <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The circuit-breaker settings are invalid.",
                nameof(options));
        }
    }
}
