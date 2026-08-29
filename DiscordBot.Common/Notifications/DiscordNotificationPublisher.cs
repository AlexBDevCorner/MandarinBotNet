using Discord;
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

public interface IDiscordNotificationChannelResolver
{
    DiscordNotificationDestination Resolve(ulong guildId, ulong channelId);
}

public interface IDiscordNotificationChannel
{
    Task SendMessageAsync(string content, bool allowEveryoneMention);
}

public sealed record DiscordNotificationDestination(
    bool GuildAvailable,
    IDiscordNotificationChannel? Channel);

public sealed class DiscordNotificationChannelResolver(
    DiscordSocketClient discordClient) : IDiscordNotificationChannelResolver
{
    public DiscordNotificationDestination Resolve(ulong guildId, ulong channelId)
    {
        var guild = discordClient.GetGuild(guildId);
        if (guild is null)
        {
            return new DiscordNotificationDestination(false, null);
        }

        var channel = guild.GetTextChannel(channelId);
        return new DiscordNotificationDestination(
            true,
            channel is null ? null : new DiscordNotificationChannel(channel));
    }

    private sealed class DiscordNotificationChannel(
        SocketTextChannel channel) : IDiscordNotificationChannel
    {
        public Task SendMessageAsync(string content, bool allowEveryoneMention)
        {
            return channel.SendMessageAsync(
                content,
                allowedMentions: allowEveryoneMention
                    ? null
                    : AllowedMentions.None);
        }
    }
}

public sealed class DiscordNotificationPublisher(
    IDiscordNotificationChannelResolver channelResolver,
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

        var destination = channelResolver.Resolve(target.GuildId, target.ChannelId);
        if (!destination.GuildAvailable)
        {
            logger.LogWarning(
                "Configured Discord guild {GuildId} is not available for event {Event}; " +
                "notification {NotificationType} finished with outcome {Outcome}.",
                target.GuildId,
                sourceIdentifier,
                notificationType,
                "SkippedGuildUnavailable");
            return false;
        }

        if (destination.Channel is null)
        {
            logger.LogWarning(
                "Configured Discord channel {ChannelId} was not found in guild {GuildId} for event {Event}; " +
                "notification {NotificationType} finished with outcome {Outcome}.",
                target.ChannelId,
                target.GuildId,
                sourceIdentifier,
                notificationType,
                "SkippedChannelUnavailable");
            return false;
        }

        var checkpoint = new NotificationCheckpoint(
            target.GuildId,
            target.ChannelId,
            sourceIdentifier,
            notificationType);

        const string everyonePrefix = "@everyone ";
        var mentionEveryone = target.MentionEveryone &&
            NotificationTypes.AllowsEveryoneMention(notificationType);
        var contentLimit = DiscordConfig.MaxMessageSize -
            (mentionEveryone ? everyonePrefix.Length : 0);
        var chunks = DiscordMessageChunker.Split(message, contentLimit);
        var sentAny = false;

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunkIndex = index;
            var partCheckpoint = chunks.Count == 1
                ? checkpoint
                : checkpoint with
                {
                    NotificationType =
                        $"{notificationType}:part-{index + 1}-of-{chunks.Count}"
                };

            var sent = await deliveryCoordinator.SendOnceAsync(
                partCheckpoint,
                async () =>
                {
                    var isFirstChunk = chunkIndex == 0;
                    var content = isFirstChunk && mentionEveryone
                        ? $"{everyonePrefix}{chunks[chunkIndex]}"
                        : chunks[chunkIndex];
                    await destination.Channel.SendMessageAsync(
                        content,
                        mentionEveryone && isFirstChunk);
                },
                cancellationToken);
            sentAny |= sent;
        }

        return sentAny;
    }
}
