using DiscordBot;
using DiscordBot.EventWatch;
using DiscordBot.Health;
using MandarinBotNet.Extensions;

if (args.FirstOrDefault() == "--health-check")
{
    if (args is not [_, var probeHealthStatePath, var maximumAgeSecondsText] ||
        !int.TryParse(maximumAgeSecondsText, out var maximumAgeSeconds) ||
        maximumAgeSeconds <= 0)
    {
        await Console.Error.WriteLineAsync(
            "Usage: --health-check <health-state-path> <maximum-age-seconds>");
        return 2;
    }

    var probeResult = await HealthStateProbe.CheckReadinessAsync(
        probeHealthStatePath,
        TimeSpan.FromSeconds(maximumAgeSeconds),
        TimeProvider.System);
    await Console.Error.WriteLineAsync(probeResult.Message);
    return probeResult.IsHealthy ? 0 : 1;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMandarinBotConfiguration(
    builder.Configuration,
    builder.Environment);
builder.Services.AddMandarinBotClients();
builder.Services.AddEventWatch();
builder.Logging.AddMandarinBotLogging();
builder.Services.AddMandarinBotCoreServices();
builder.Services.AddMandarinBotDiscordServices();
builder.Services.AddMandarinBotStorage();
builder.Services.AddMandarinBotHealthChecks();
builder.Services.AddHostedService<DiscordBotHostedService>();
builder.Services.AddMandarinBotScheduling();

var host = builder.Build();
await host.RunAsync();
return 0;
