using Discord.WebSocket;
using Discord;
using DiscordBot;
using DiscordBot.Jobs;
using DiscordBot.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var discordClient = new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged
});
builder.Services.AddSingleton(discordClient);

var readinessTimeout = int.TryParse(
    builder.Configuration["DISCORD_READINESS_TIMEOUT_SECONDS"],
    out var readinessTimeoutSeconds)
    ? TimeSpan.FromSeconds(readinessTimeoutSeconds)
    : TimeSpan.FromSeconds(30);
var discordSettings = new DiscordBotSettings(
    builder.Configuration["BOT_TOKEN"],
    readinessTimeout);
builder.Services.AddSingleton(discordSettings);
builder.Services.AddSingleton<DiscordConnectionReadiness>();
builder.Services.AddSingleton<IDiscordConnectionReadiness>(
    services => services.GetRequiredService<DiscordConnectionReadiness>());
builder.Services.AddSingleton<IDiscordGatewayConnection, DiscordGatewayConnection>();

var notificationDatabasePath = Path.Combine(
    AppContext.BaseDirectory,
    "data",
    "notification-state.db");
builder.Services.AddSingleton<INotificationCheckpointStore>(
    new SqliteNotificationCheckpointStore(notificationDatabasePath));
builder.Services.AddSingleton<NotificationDeliveryCoordinator>();


builder.Services.AddHostedService<DiscordBotHostedService>();

builder.Services.AddQuartz(q =>
{
    var jobs = new List<(string JobName, bool IsEnabled)>
    {
        new (Jobs.PremierLeagueNotificationJob, true),
        new (Jobs.PremierLeagueClassicStandingsInformationJob, true),
        new (Jobs.PremierLeagueH2hStandingsInformationJob, true)
    };

    if (jobs.Any(j => j.JobName == Jobs.PremierLeagueNotificationJob && j.IsEnabled))
    {
        q.AddJob<PremierLeagueNotificationJob>(
            j => j.WithIdentity(JobSchedules.PremierLeagueNotificationJobKey));
        q.AddTrigger(trigger => trigger
            .ForJob(JobSchedules.PremierLeagueNotificationJobKey)
            .WithIdentity(JobSchedules.PremierLeagueNotificationTriggerName)
            .WithCronSchedule(
                JobSchedules.PremierLeagueNotificationCron,
                schedule => schedule
                    .InTimeZone(JobSchedules.RigaTimeZone)
                    .WithMisfireHandlingInstructionDoNothing()));
    }

    if (jobs.Any(j => j.JobName == Jobs.PremierLeagueClassicStandingsInformationJob && j.IsEnabled))
    {
        q.AddJob<PremierLeagueClassicStandingsInformationJob>(
            j => j.WithIdentity(JobSchedules.PremierLeagueClassicStandingsInformationJobKey));
        q.AddTrigger(trigger => trigger
            .ForJob(JobSchedules.PremierLeagueClassicStandingsInformationJobKey)
            .WithIdentity(JobSchedules.PremierLeagueClassicStandingsInformationTriggerName)
            .WithCronSchedule(
                JobSchedules.StandingsCron,
                schedule => schedule
                    .InTimeZone(JobSchedules.RigaTimeZone)
                    .WithMisfireHandlingInstructionDoNothing()));
    }

    if (jobs.Any(j => j.JobName == Jobs.PremierLeagueH2hStandingsInformationJob && j.IsEnabled))
    {
        q.AddJob<PremierLeagueH2hStandingsInformationJob>(
            j => j.WithIdentity(JobSchedules.PremierLeagueH2hStandingsInformationJobKey));
        q.AddTrigger(trigger => trigger
            .ForJob(JobSchedules.PremierLeagueH2hStandingsInformationJobKey)
            .WithIdentity(JobSchedules.PremierLeagueH2hStandingsInformationTriggerName)
            .WithCronSchedule(
                JobSchedules.StandingsCron,
                schedule => schedule
                    .InTimeZone(JobSchedules.RigaTimeZone)
                    .WithMisfireHandlingInstructionDoNothing()));
    }
})
.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var host = builder.Build();
await host.RunAsync();
