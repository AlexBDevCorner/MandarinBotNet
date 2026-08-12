using Discord;
using Discord.WebSocket;
using DiscordBot.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot;

public static class DiscordGatewayServiceCollectionExtensions
{
    public static IServiceCollection AddDiscordGateway(this IServiceCollection services)
    {
        services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged |
                GatewayIntents.GuildMembers
        }));
        services.AddSingleton<IDiscordGatewayConnection, DiscordGatewayConnection>();
        services.AddSingleton<
            IDiscordNotificationChannelResolver,
            DiscordNotificationChannelResolver>();

        return services;
    }
}
