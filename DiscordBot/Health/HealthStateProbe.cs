using System.Text.Json;

namespace DiscordBot.Health;

public static class HealthStateProbe
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<HealthProbeResult> CheckReadinessAsync(
        string healthStatePath,
        TimeSpan maximumAge,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        if (maximumAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAge),
                maximumAge,
                "The maximum health state age must be greater than zero.");
        }

        try
        {
            await using var stream = File.OpenRead(healthStatePath);
            var state = await JsonSerializer.DeserializeAsync<PublishedHealthState>(
                stream,
                SerializerOptions,
                cancellationToken);

            if (state is null || state.SchemaVersion != 1)
            {
                return HealthProbeResult.Unhealthy("The health state schema is invalid.");
            }

            var age = timeProvider.GetUtcNow() - state.ObservedAtUtc;
            if (age < TimeSpan.Zero || age > maximumAge)
            {
                return HealthProbeResult.Unhealthy("The health state is stale.");
            }

            if (!state.Live)
            {
                return HealthProbeResult.Unhealthy("The worker is not live.");
            }

            if (!state.Ready)
            {
                return HealthProbeResult.Unhealthy("One or more readiness checks are unhealthy.");
            }

            return HealthProbeResult.Healthy();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return HealthProbeResult.Unhealthy(
                $"The health state could not be read: {exception.Message}");
        }
    }
}

public sealed record HealthProbeResult(bool IsHealthy, string Message)
{
    public static HealthProbeResult Healthy() => new(true, "The worker is ready.");

    public static HealthProbeResult Unhealthy(string message) => new(false, message);
}
