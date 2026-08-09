using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DiscordBot.Health;

public sealed class DiscordReadinessHealthCheck(
    IDiscordConnectionReadiness readiness) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = readiness.IsReady
            ? HealthCheckResult.Healthy("The Discord gateway is ready.")
            : HealthCheckResult.Unhealthy("The Discord gateway is disconnected or not ready.");

        return Task.FromResult(result);
    }
}
