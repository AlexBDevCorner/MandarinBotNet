using Discord.WebSocket;
using Discord;
using DiscordBot;
using DiscordBot.Jobs;
using DiscordBot.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using TimeZoneConverter;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged
}));

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

    var rigaTimeZone = TZConvert.GetTimeZoneInfo("Europe/Riga");

    var targetTime = new DateTimeOffset(
        DateTime.Today.Year,
        DateTime.Today.Month,
        DateTime.Today.Day,
        17, 0, 0,
        rigaTimeZone.BaseUtcOffset
    );

    if (jobs.Any(j => j.JobName == Jobs.PremierLeagueNotificationJob && j.IsEnabled))
    {
        var PremierLeagueNotificationJobKey = new JobKey("PremierLeagueNotification");
        q.AddJob<PremierLeagueNotificationJob>(j => j.WithIdentity(PremierLeagueNotificationJobKey));
        q.AddTrigger(t => t
            .ForJob(PremierLeagueNotificationJobKey)
            .WithIdentity("PremierLeagueNotificationTrigger")
            .StartAt(targetTime)
            .WithSimpleSchedule(s => s.WithIntervalInHours(1).RepeatForever()));
    }

    if (jobs.Any(j => j.JobName == Jobs.PremierLeagueClassicStandingsInformationJob && j.IsEnabled))
    {
        var PremierLeagueClassicStandingsInformationJobKey = new JobKey("PremierLeagueClassicStandingsInformation");
        q.AddJob<PremierLeagueClassicStandingsInformationJob>(j => j.WithIdentity(PremierLeagueClassicStandingsInformationJobKey));
        q.AddTrigger(t => t
            .ForJob(PremierLeagueClassicStandingsInformationJobKey)
            .WithIdentity("PremierLeagueClassicStandingsInformationTrigger")
            .StartAt(targetTime)
            .WithSimpleSchedule(s => s.WithIntervalInHours(24).RepeatForever()));
    }

    if (jobs.Any(j => j.JobName == Jobs.PremierLeagueH2hStandingsInformationJob && j.IsEnabled))
    {
        var PremierLeagueH2hStandingsInformationJobKey = new JobKey("PremierLeagueH2hStandingsInformation");
        q.AddJob<PremierLeagueH2hStandingsInformationJob>(j => j.WithIdentity(PremierLeagueH2hStandingsInformationJobKey));
        q.AddTrigger(t => t
            .ForJob(PremierLeagueH2hStandingsInformationJobKey)
            .WithIdentity("PremierLeagueH2hStandingsInformationTrigger")
            .StartAt(targetTime)
            .WithSimpleSchedule(s => s.WithIntervalInHours(24).RepeatForever()));
    }
})
.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var host = builder.Build();
await host.RunAsync();
