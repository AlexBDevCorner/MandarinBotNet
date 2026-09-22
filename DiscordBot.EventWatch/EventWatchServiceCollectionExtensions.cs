using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace DiscordBot.EventWatch;

public static class EventWatchServiceCollectionExtensions
{
    public static IServiceCollection AddEventWatch(
        this IServiceCollection services,
        RigaFcClientOptions? clientOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        clientOptions ??= new RigaFcClientOptions();
        Validate(clientOptions);
        services.TryAddSingleton(clientOptions);

        services.TryAddSingleton<RigaFcPageParser>();
        services.TryAddSingleton<EventWatchSignalDetector>();
        services.TryAddSingleton<EventWatchMessageCompositionService>();
        services.TryAddSingleton<IEventWatchSource, RigaFcEventWatchSource>();
        services.TryAddSingleton<EventWatchService>();

        var clientBuilder = services.AddHttpClient<RigaFcClient>(client =>
        {
            client.BaseAddress = clientOptions.BaseAddress;
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(RigaFcUserAgent.Value);
        });

        clientBuilder.AddStandardResilienceHandler(resilience =>
        {
            resilience.AttemptTimeout.Timeout = clientOptions.AttemptTimeout;
            resilience.TotalRequestTimeout.Timeout = clientOptions.TotalRequestTimeout;

            resilience.Retry.MaxRetryAttempts = clientOptions.MaxRetryAttempts;
            resilience.Retry.Delay = clientOptions.RetryDelay;
            resilience.Retry.BackoffType = DelayBackoffType.Exponential;
            resilience.Retry.UseJitter = clientOptions.UseRetryJitter;

            resilience.CircuitBreaker.FailureRatio =
                clientOptions.CircuitBreakerFailureRatio;
            resilience.CircuitBreaker.MinimumThroughput =
                clientOptions.CircuitBreakerMinimumThroughput;
            resilience.CircuitBreaker.SamplingDuration =
                clientOptions.CircuitBreakerSamplingDuration;
            resilience.CircuitBreaker.BreakDuration =
                clientOptions.CircuitBreakerBreakDuration;
        });

        return services;
    }

    private static void Validate(RigaFcClientOptions options)
    {
        if (!options.BaseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException(
                "The Riga FC base address must be absolute.",
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
