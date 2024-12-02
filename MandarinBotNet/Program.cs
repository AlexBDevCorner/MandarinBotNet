using Discord.WebSocket;
using Discord;
using DiscordBot;
using DiscordBot.Jobs;
using Quartz;
using TimeZoneConverter;
using DiscordBot.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged
}));

builder.Services.AddSingleton(new H2hMatchesPlayedService { MatchesPlayed = 0 });


builder.Services.AddHostedService<DiscordBotHostedService>();

builder.Services.AddQuartz(q =>
{
    var jobs = new List<(string JobName, bool IsEnabled)>
    {
        new (Jobs.SelfPingerJob, true),
        new (Jobs.PremierLeagueNotificationJob, true),
        new (Jobs.PremierLeagueClassicStandingsInformationJob, true),
        new (Jobs.PremierLeagueH2hStandingsInformationJob, true)
    };

    if (jobs.Any(j => j.JobName == Jobs.SelfPingerJob && j.IsEnabled))
    {
        var selfPingerJobKey = new JobKey("SelfPinger");
        q.AddJob<SelfPinger>(j => j.WithIdentity(selfPingerJobKey));
        q.AddTrigger(t => t
            .ForJob(selfPingerJobKey)
            .WithIdentity("SelfPingerTrigger")
            .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));
    }
    

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();