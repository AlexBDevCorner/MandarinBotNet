using Discord.WebSocket;
using Discord;
using DiscordBot;
using DiscordBot.Jobs;
using Quartz;

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


builder.Services.AddHostedService<DiscordBotHostedService>();

builder.Services.AddQuartz(q =>
{
    var selfPingerJobKey = new JobKey("SelfPinger");
    q.AddJob<SelfPinger>(j => j.WithIdentity(selfPingerJobKey));
    q.AddTrigger(t => t
        .ForJob(selfPingerJobKey)
        .WithIdentity("SelfPingerTrigger")
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));

    var PremierLeagueNotificationJobKey = new JobKey("PremierLeagueNotification");
    q.AddJob<PremierLeagueNotificationJob>(j => j.WithIdentity(PremierLeagueNotificationJobKey));
    q.AddTrigger(t => t
        .ForJob(PremierLeagueNotificationJobKey)
        .WithIdentity("PremierLeagueNotificationTrigger")
        .StartNow()
        .WithSimpleSchedule(s => s.WithRepeatCount(0)));
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
