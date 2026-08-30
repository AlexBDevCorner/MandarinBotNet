using DiscordBot.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MandarinBotNet.Extensions;

public static class MandarinBotHealthCheckExtensions
{
    public static IServiceCollection AddMandarinBotHealthChecks(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<DiscordReadinessHealthCheck>();
        services.AddHealthChecks()
            .AddCheck<DiscordReadinessHealthCheck>(
                "discord_gateway",
                tags: ["ready"])
            .AddCheck(
                "sqlite_storage",
                new SqliteStorageHealthCheck(
                    MandarinBotDataPaths.NotificationDatabasePath),
                tags: ["ready"])
            .AddCheck(
                "fpl_statistics_storage",
                new SqliteStorageHealthCheck(
                    MandarinBotDataPaths.FplStatisticsDatabasePath),
                tags: ["ready"])
            .AddCheck(
                "fpl_recognition_storage",
                new SqliteStorageHealthCheck(
                    MandarinBotDataPaths.FplRecognitionDatabasePath),
                tags: ["ready"])
            .AddCheck(
                "fpl_price_snapshot_storage",
                new SqliteStorageHealthCheck(
                    MandarinBotDataPaths.FplPriceSnapshotDatabasePath),
                tags: ["ready"])
            .AddCheck(
                "fpl_live_notification_storage",
                new SqliteStorageHealthCheck(
                    MandarinBotDataPaths.FplLiveNotificationDatabasePath),
                tags: ["ready"]);
        services.AddSingleton<IHealthCheckPublisher>(serviceProvider =>
            new FileHealthCheckPublisher(
                MandarinBotDataPaths.HealthStatePath,
                serviceProvider.GetRequiredService<TimeProvider>()));
        services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay = TimeSpan.Zero;
            options.Period = TimeSpan.FromSeconds(5);
            options.Timeout = TimeSpan.FromSeconds(4);
            options.Predicate = registration => registration.Tags.Contains("ready");
        });

        return services;
    }
}
