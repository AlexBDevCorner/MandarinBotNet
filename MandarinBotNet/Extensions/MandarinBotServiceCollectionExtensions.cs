using DiscordBot;
using DiscordBot.BenchWarming;
using DiscordBot.Commands;
using DiscordBot.Deadlines;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Chips;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Live;
using DiscordBot.FantasyPremierLeague.PriceChanges;
using DiscordBot.FantasyPremierLeague.Recap;
using DiscordBot.FantasyPremierLeague.Recognition;
using DiscordBot.FantasyPremierLeague.Standings;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using DiscordBot.UclFantasy;
using DiscordBot.WelcomeMessages;
using Microsoft.Extensions.DependencyInjection;

namespace MandarinBotNet.Extensions;

public static class MandarinBotServiceCollectionExtensions
{
    public static IServiceCollection AddMandarinBotClients(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFantasyPremierLeagueClient();
        services.AddUclFantasyClient();

        return services;
    }

    public static IServiceCollection AddMandarinBotCoreServices(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ConfiguredTimeZone>();
        services.AddSingleton<DeadlineSelectionService>();
        services.AddSingleton<ReminderEligibilityService>();
        services.AddSingleton<StandingsPublicationEligibilityService>();
        services.AddSingleton<StandingsChangeService>();
        services.AddSingleton<WinnerSelectionService>();
        services.AddSingleton<PremierLeagueMessageCompositionService>();
        services.AddSingleton<UclFantasyMessageCompositionService>();

        return services;
    }

    public static IServiceCollection AddMandarinBotDiscordServices(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDiscordGateway();

        services.AddSingleton<DiscordConnectionReadiness>();
        services.AddSingleton<IDiscordConnectionReadiness>(
            serviceProvider => serviceProvider
                .GetRequiredService<DiscordConnectionReadiness>());
        services.AddSingleton<DiscordNetLogHandler>();
        services.AddSingleton(serviceProvider =>
        {
            var commandOptions = serviceProvider
                .GetRequiredService<DiscordOptions>()
                .Commands;
            return new DiscordCommandRegistrationOptions(
                commandOptions.RegistrationMode,
                commandOptions.GuildId);
        });
        services.AddSingleton<
            IDiscordApplicationCommandClient,
            DiscordApplicationCommandClient>();
        services.AddSingleton<
            IDiscordCommandSynchronizer,
            DiscordCommandSynchronizer>();
        services.AddSingleton<DiscordCommandRegistrationCoordinator>();
        services.AddSingleton<
            IUpcomingDeadlineProvider,
            FantasyPremierLeagueDeadlineProvider>();
        services.AddSingleton<UclFantasyDeadlineProvider>();
        services.AddSingleton<IUpcomingDeadlineProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<UclFantasyDeadlineProvider>());
        services.AddSingleton<IDeadlineCommandHandler, DeadlineCommandHandler>();
        services.AddSingleton<IStandingsCommandHandler, StandingsCommandHandler>();
        services.AddSingleton<IBenchLeagueCommandHandler, BenchLeagueCommandHandler>();
        services.AddSingleton<BenchWarmingQueryService>();
        services.AddSingleton<BenchWarmingMessageComposer>();
        services.AddSingleton<BenchWarmingLeagueCalculationService>();
        services.AddSingleton<FplStatisticsCollectionService>();
        services.AddSingleton<FplGameweekRecapCalculationService>();
        services.AddSingleton<FplGameweekRecapService>();
        services.AddSingleton<FplAchievementCalculationService>();
        services.AddSingleton<FplRecognitionService>();
        services.AddSingleton<FplRecognitionQueryService>();
        services.AddSingleton<FplRecognitionMessageCompositionService>();
        services.AddSingleton<FplLiveInsightsCalculationService>();
        services.AddSingleton<FplLiveInsightsService>();
        services.AddSingleton<FplLiveInsightsMessageComposer>();
        services.AddSingleton<FplPriceChangeService>();
        services.AddSingleton<FplPriceChangeMessageCompositionService>();
        services.AddSingleton<FplStandingsSelectionService>();
        services.AddSingleton<ILiveInsightsCommandHandler, LiveInsightsCommandHandler>();
        services.AddSingleton<IProfileCommandHandler, ProfileCommandHandler>();
        services.AddSingleton<IAchievementsCommandHandler, AchievementsCommandHandler>();
        services.AddSingleton<FplChipCatalog>();
        services.AddSingleton<FplChipMessageCompositionService>();
        services.AddSingleton<IChipsCommandHandler, ChipsCommandHandler>();
        services.AddSingleton<WelcomeMessageTemplateRotator>();
        services.AddSingleton<
            IWelcomeMessageDestinationResolver,
            WelcomeMessageDestinationResolver>();
        services.AddSingleton<IWelcomeMessageHandler, WelcomeMessageHandler>();
        services.AddSingleton(new WelcomeMessageAsset(Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "pc7n1.jpg")));

        return services;
    }

    public static IServiceCollection AddMandarinBotStorage(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<INotificationCheckpointStore>(
            new SqliteNotificationCheckpointStore(
                MandarinBotDataPaths.NotificationDatabasePath));
        services.AddSingleton<IBenchWarmingLeagueStore>(
            new SqliteBenchWarmingLeagueStore(
                MandarinBotDataPaths.BenchWarmingDatabasePath));
        services.AddSingleton<IFplStatisticsStore>(
            new SqliteFplStatisticsStore(
                MandarinBotDataPaths.FplStatisticsDatabasePath));
        services.AddSingleton<IFplRecognitionStore>(
            new SqliteFplRecognitionStore(
                MandarinBotDataPaths.FplRecognitionDatabasePath));
        services.AddSingleton<IFplPriceSnapshotStore>(
            new SqliteFplPriceSnapshotStore(
                MandarinBotDataPaths.FplPriceSnapshotDatabasePath));
        services.AddSingleton<NotificationDeliveryCoordinator>();
        services.AddSingleton<
            IDiscordNotificationPublisher,
            DiscordNotificationPublisher>();

        return services;
    }
}
