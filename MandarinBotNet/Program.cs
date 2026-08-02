using Discord.WebSocket;
using Discord;
using DiscordBot;
using DiscordBot.Commands;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.Jobs;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

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

var discordClient = new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged
});
builder.Services.AddSingleton(discordClient);

builder.Services.AddSingleton<DiscordConnectionReadiness>();
builder.Services.AddSingleton<IDiscordConnectionReadiness>(
    services => services.GetRequiredService<DiscordConnectionReadiness>());
builder.Services.AddSingleton<IDiscordGatewayConnection, DiscordGatewayConnection>();
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
