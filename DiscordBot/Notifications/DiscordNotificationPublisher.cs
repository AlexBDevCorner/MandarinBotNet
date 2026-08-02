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
                "Configured Discord guild {GuildId} is not available for event {Event}; " +
                "notification {NotificationType} finished with outcome {Outcome}.",
                target.GuildId,
                sourceIdentifier,
                notificationType,
                "SkippedGuildUnavailable");
            return false;
        }

        var channel = guild.GetTextChannel(target.ChannelId);
        if (channel is null)
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
        var contentLimit = DiscordConfig.MaxMessageSize -
            (target.MentionEveryone ? everyonePrefix.Length : 0);
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
                    var content = isFirstChunk
                        ? target.FormatMessage(chunks[chunkIndex])
                        : chunks[chunkIndex];
                    var allowedMentions = target.MentionEveryone && isFirstChunk
                        ? null
                        : AllowedMentions.None;

                    await channel.SendMessageAsync(
                        content,
                        allowedMentions: allowedMentions);
                },
                cancellationToken);
            sentAny |= sent;
        }

        return sentAny;
    }
}
