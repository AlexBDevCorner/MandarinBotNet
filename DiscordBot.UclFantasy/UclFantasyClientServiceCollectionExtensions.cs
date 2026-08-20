using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace DiscordBot.UclFantasy;

public static class UclFantasyClientServiceCollectionExtensions
{
    public static IHttpClientBuilder AddUclFantasyClient(
        this IServiceCollection services,
        UclFantasyClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        options ??= new UclFantasyClientOptions();
        Validate(options);
        services.TryAddSingleton(options);

        var clientBuilder = services.AddHttpClient<
            IUclFantasyClient,
            UclFantasyClient>(client =>
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

    private static void Validate(UclFantasyClientOptions options)
    {
        if (!options.BaseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException(
                "The UCL Fantasy base address must be absolute.",
                nameof(options));
        }

        if (options.AttemptTimeout <= TimeSpan.Zero ||
            options.TotalRequestTimeout <= options.AttemptTimeout)
        {
            throw new ArgumentException(
                "The total request timeout must be greater than the positive attempt timeout.",
                nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.WebConfigurationPath) ||
            string.IsNullOrWhiteSpace(options.FeedBasePath) ||
            string.IsNullOrWhiteSpace(options.FixturesPathTemplate) ||
            string.IsNullOrWhiteSpace(options.Language))
        {
            throw new ArgumentException(
                "UCL Fantasy feed paths and language are required.",
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
