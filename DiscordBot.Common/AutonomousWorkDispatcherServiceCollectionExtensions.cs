using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot;

public static class AutonomousWorkDispatcherServiceCollectionExtensions
{
    public static IHttpClientBuilder AddAutonomousWorkDispatcherClient(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Deliberately no resilience retries: the next normal Quartz occurrence
        // (every 10 minutes) is the retry mechanism.
        return services.AddHttpClient<IAutonomousWorkDispatcherClient, AutonomousWorkDispatcherClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MandarinBotNet");
        });
    }
}
