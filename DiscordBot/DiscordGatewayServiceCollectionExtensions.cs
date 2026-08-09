using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot;

public static class DiscordGatewayServiceCollectionExtensions
{
    public static IServiceCollection AddDiscordGateway(this IServiceCollection services)
    {
        services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged
        }));
        services.AddSingleton<IDiscordGatewayConnection, DiscordGatewayConnection>();

        return services;
    }
}
