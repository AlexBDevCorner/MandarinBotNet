using DiscordBot;
using DiscordBot.Commands;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.Health;
using DiscordBot.Jobs;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using DiscordBot.WelcomeMessages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

if (args.FirstOrDefault() == "--health-check")
{
    if (args is not [_, var probeHealthStatePath, var maximumAgeSecondsText] ||
        !int.TryParse(maximumAgeSecondsText, out var maximumAgeSeconds) ||
        maximumAgeSeconds <= 0)
    {
        Console.Error.WriteLine(
            "Usage: --health-check <health-state-path> <maximum-age-seconds>");
        return 2;
    }

    var probeResult = await HealthStateProbe.CheckReadinessAsync(
        probeHealthStatePath,
        TimeSpan.FromSeconds(maximumAgeSeconds),
        TimeProvider.System);
    Console.Error.WriteLine(probeResult.Message);
    return probeResult.IsHealthy ? 0 : 1;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMandarinBotConfiguration(
    builder.Configuration,
    builder.Environment);
builder.Services.AddFantasyPremierLeagueClient();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ConfiguredTimeZone>();
builder.Services.AddSingleton<DeadlineSelectionService>();
builder.Services.AddSingleton<ReminderEligibilityService>();
builder.Services.AddSingleton<StandingsPublicationEligibilityService>();
builder.Services.AddSingleton<StandingsChangeService>();
builder.Services.AddSingleton<WinnerSelectionService>();
builder.Services.AddSingleton<PremierLeagueMessageCompositionService>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddDiscordGateway();

builder.Services.AddSingleton<DiscordConnectionReadiness>();
builder.Services.AddSingleton<IDiscordConnectionReadiness>(
    services => services.GetRequiredService<DiscordConnectionReadiness>());
builder.Services.AddSingleton<DiscordNetLogHandler>();
builder.Services.AddSingleton(services =>
{
    var commandOptions = services
        .GetRequiredService<DiscordOptions>()
        .Commands;
    return new DiscordCommandRegistrationOptions(
        commandOptions.RegistrationMode,
        commandOptions.GuildId);
});
builder.Services.AddSingleton<
    IDiscordApplicationCommandClient,
    DiscordApplicationCommandClient>();
builder.Services.AddSingleton<IDiscordCommandSynchronizer, DiscordCommandSynchronizer>();
builder.Services.AddSingleton<DiscordCommandRegistrationCoordinator>();
builder.Services.AddSingleton<IStandingsCommandHandler, StandingsCommandHandler>();
builder.Services.AddSingleton<WelcomeMessageTemplateRotator>();
builder.Services.AddSingleton<
    IWelcomeMessageDestinationResolver,
    WelcomeMessageDestinationResolver>();
builder.Services.AddSingleton<IWelcomeMessageHandler, WelcomeMessageHandler>();
builder.Services.AddSingleton(new WelcomeMessageAsset(Path.Combine(
    AppContext.BaseDirectory,
    "Assets",
    "pc7n1.jpg")));

var notificationDatabasePath = Path.Combine(
    AppContext.BaseDirectory,
    "data",
    "notification-state.db");
builder.Services.AddSingleton<INotificationCheckpointStore>(
    new SqliteNotificationCheckpointStore(notificationDatabasePath));
builder.Services.AddSingleton<NotificationDeliveryCoordinator>();
builder.Services.AddSingleton<
    IDiscordNotificationPublisher,
    DiscordNotificationPublisher>();

var healthStatePath = Path.Combine(
    AppContext.BaseDirectory,
    "data",
    "health-state.json");
builder.Services.AddSingleton<DiscordReadinessHealthCheck>();
builder.Services.AddSingleton(new SqliteStorageHealthCheck(notificationDatabasePath));
builder.Services.AddHealthChecks()
    .AddCheck<DiscordReadinessHealthCheck>(
        "discord_gateway",
        tags: ["ready"])
    .AddCheck<SqliteStorageHealthCheck>(
        "sqlite_storage",
        tags: ["ready"]);
builder.Services.AddSingleton<IHealthCheckPublisher>(services =>
    new FileHealthCheckPublisher(
        healthStatePath,
        services.GetRequiredService<TimeProvider>()));
builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Delay = TimeSpan.Zero;
    options.Period = TimeSpan.FromSeconds(5);
    options.Timeout = TimeSpan.FromSeconds(4);
    options.Predicate = registration => registration.Tags.Contains("ready");
});


builder.Services.AddHostedService<DiscordBotHostedService>();

builder.Services.AddQuartz();
builder.Services.AddOptions<QuartzOptions>()
    .Configure<IOptions<MandarinBotOptions>>((q, botOptions) =>
{
    var schedules = botOptions.Value.Schedules;
    var timeZone = JobSchedules.GetTimeZone(schedules);

    if (schedules.PremierLeagueNotifications.Enabled)
    {
        q.AddJob<PremierLeagueNotificationJob>(
            job => job.WithIdentity(JobSchedules.PremierLeagueNotificationJobKey));
        q.AddTrigger(trigger => trigger
            .ForJob(JobSchedules.PremierLeagueNotificationJobKey)
            .WithIdentity(JobSchedules.PremierLeagueNotificationTriggerName)
            .WithCronSchedule(
                schedules.PremierLeagueNotifications.Cron,
                schedule => schedule
                    .InTimeZone(timeZone)
                    .WithMisfireHandlingInstructionDoNothing()));
    }

    if (schedules.ClassicStandings.Enabled)
    {
        q.AddJob<PremierLeagueClassicStandingsInformationJob>(
            job => job.WithIdentity(
                JobSchedules.PremierLeagueClassicStandingsInformationJobKey));
        q.AddTrigger(trigger => trigger
            .ForJob(JobSchedules.PremierLeagueClassicStandingsInformationJobKey)
            .WithIdentity(
                JobSchedules.PremierLeagueClassicStandingsInformationTriggerName)
            .WithCronSchedule(
                schedules.ClassicStandings.Cron,
                schedule => schedule
                    .InTimeZone(timeZone)
                    .WithMisfireHandlingInstructionDoNothing()));
    }

    if (schedules.HeadToHeadStandings.Enabled)
    {
        q.AddJob<PremierLeagueH2hStandingsInformationJob>(
            job => job.WithIdentity(
                JobSchedules.PremierLeagueH2hStandingsInformationJobKey));
        q.AddTrigger(trigger => trigger
            .ForJob(JobSchedules.PremierLeagueH2hStandingsInformationJobKey)
            .WithIdentity(
                JobSchedules.PremierLeagueH2hStandingsInformationTriggerName)
            .WithCronSchedule(
                schedules.HeadToHeadStandings.Cron,
                schedule => schedule
                    .InTimeZone(timeZone)
                    .WithMisfireHandlingInstructionDoNothing()));
    }
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var host = builder.Build();
await host.RunAsync();
return 0;
