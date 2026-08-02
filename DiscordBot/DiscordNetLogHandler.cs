using Discord;
using Microsoft.Extensions.Logging;

namespace DiscordBot;

public sealed class DiscordNetLogHandler(
    ILogger<DiscordNetLogHandler> logger)
{
    public Task HandleAsync(LogMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        logger.Log(
            ToLogLevel(message.Severity),
            message.Exception,
            "Discord.Net event {Event} reported: {DiscordMessage}",
            message.Source,
            message.Message);

        return Task.CompletedTask;
    }

    public static LogLevel ToLogLevel(LogSeverity severity)
    {
        return severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Information
        };
    }
}
