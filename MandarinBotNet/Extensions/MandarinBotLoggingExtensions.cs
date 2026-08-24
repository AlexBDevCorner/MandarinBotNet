using Microsoft.Extensions.Logging;

namespace MandarinBotNet.Extensions;

public static class MandarinBotLoggingExtensions
{
    public static ILoggingBuilder AddMandarinBotLogging(
        this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);

        logging.ClearProviders();
        logging.AddConsole();
        logging.AddDebug();

        return logging;
    }
}
