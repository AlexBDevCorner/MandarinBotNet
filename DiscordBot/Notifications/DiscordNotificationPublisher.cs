using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Notifications;

public interface IDiscordNotificationPublisher
{
    Task<bool> PublishOnceAsync(
        NotificationTargetOptions target,
        string sourceIdentifier,
        string notificationType,
        string message,
        CancellationToken cancellationToken);
}

public sealed class DiscordNotificationPublisher(
    DiscordSocketClient discordClient,
    NotificationDeliveryCoordinator deliveryCoordinator,
    ILogger<DiscordNotificationPublisher> logger) : IDiscordNotificationPublisher
{
    public async Task<bool> PublishOnceAsync(
        NotificationTargetOptions target,
        string sourceIdentifier,
        string notificationType,
        string message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationType);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        cancellationToken.ThrowIfCancellationRequested();

        var guild = discordClient.GetGuild(target.GuildId);
        if (guild is null)
        {
            logger.LogWarning(
                "Configured Discord guild {GuildId} is not available; notification {NotificationType} was skipped.",
                target.GuildId,
                notificationType);
            return false;
        }

        var channel = guild.GetTextChannel(target.ChannelId);
        if (channel is null)
        {
            logger.LogWarning(
                "Configured Discord channel {ChannelId} was not found in guild {GuildId}; notification {NotificationType} was skipped.",
                target.ChannelId,
                target.GuildId,
                notificationType);
            return false;
        }

        var checkpoint = new NotificationCheckpoint(
            target.GuildId,
            target.ChannelId,
            sourceIdentifier,
            notificationType);

        return await deliveryCoordinator.SendOnceAsync(
            checkpoint,
            () => channel.SendMessageAsync(target.FormatMessage(message)),
            cancellationToken);
    }
}
