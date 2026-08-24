using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Notifications
{
    public sealed class NotificationDeliveryCoordinator(
        INotificationCheckpointStore checkpointStore,
        TimeProvider timeProvider,
        ILogger<NotificationDeliveryCoordinator> logger)
    {
        private readonly ConcurrentDictionary<NotificationCheckpoint, SemaphoreSlim> _deliveryLocks = new();
        private readonly ConcurrentDictionary<NotificationCheckpoint, int> _deliveryAttempts = new();

        public async Task<bool> SendOnceAsync(
            NotificationCheckpoint checkpoint,
            Func<Task> sendAsync,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(checkpoint);
            ArgumentNullException.ThrowIfNull(sendAsync);

            var deliveryLock = _deliveryLocks.GetOrAdd(checkpoint, static _ => new SemaphoreSlim(1, 1));
            await deliveryLock.WaitAsync(cancellationToken);
            var attempt = _deliveryAttempts.AddOrUpdate(
                checkpoint,
                1,
                static (_, currentAttempt) => currentAttempt + 1);
            var startedTimestamp = timeProvider.GetTimestamp();

            try
            {
                if (checkpointStore.IsDelivered(checkpoint))
                {
                    _deliveryAttempts.TryRemove(checkpoint, out _);
                    logger.LogInformation(
                        "Notification checkpoint decision for guild {GuildId}, channel {ChannelId}, " +
                        "event {Event}, notification {NotificationType}, attempt {Attempt}: {Outcome} " +
                        "after {DurationMs} ms",
                        checkpoint.GuildId,
                        checkpoint.ChannelId,
                        checkpoint.SourceIdentifier,
                        checkpoint.NotificationType,
                        attempt,
                        "AlreadyDelivered",
                        GetDurationMilliseconds(startedTimestamp));
                    return false;
                }

                logger.LogDebug(
                    "Notification delivery started for guild {GuildId}, channel {ChannelId}, " +
                    "event {Event}, notification {NotificationType}, attempt {Attempt}",
                    checkpoint.GuildId,
                    checkpoint.ChannelId,
                    checkpoint.SourceIdentifier,
                    checkpoint.NotificationType,
                    attempt);

                cancellationToken.ThrowIfCancellationRequested();
                await sendAsync();
                checkpointStore.RecordDelivered(checkpoint, timeProvider.GetUtcNow());
                _deliveryAttempts.TryRemove(checkpoint, out _);

                logger.LogInformation(
                    "Notification delivery finished for guild {GuildId}, channel {ChannelId}, " +
                    "event {Event}, notification {NotificationType}, attempt {Attempt} with outcome {Outcome} " +
                    "in {DurationMs} ms; checkpoint recorded",
                    checkpoint.GuildId,
                    checkpoint.ChannelId,
                    checkpoint.SourceIdentifier,
                    checkpoint.NotificationType,
                    attempt,
                    "DeliveredAndCheckpointed",
                    GetDurationMilliseconds(startedTimestamp));
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation(
                    "Notification delivery canceled for guild {GuildId}, channel {ChannelId}, " +
                    "event {Event}, notification {NotificationType}, attempt {Attempt} with outcome {Outcome} " +
                    "after {DurationMs} ms",
                    checkpoint.GuildId,
                    checkpoint.ChannelId,
                    checkpoint.SourceIdentifier,
                    checkpoint.NotificationType,
                    attempt,
                    "Canceled",
                    GetDurationMilliseconds(startedTimestamp));
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Notification delivery failed for guild {GuildId}, channel {ChannelId}, " +
                    "event {Event}, notification {NotificationType}, attempt {Attempt} with outcome {Outcome} " +
                    "after {DurationMs} ms; checkpoint not recorded",
                    checkpoint.GuildId,
                    checkpoint.ChannelId,
                    checkpoint.SourceIdentifier,
                    checkpoint.NotificationType,
                    attempt,
                    "Failed",
                    GetDurationMilliseconds(startedTimestamp));
                throw;
            }
            finally
            {
                deliveryLock.Release();
            }
        }

        private double GetDurationMilliseconds(long startedTimestamp)
        {
            return Math.Round(
                timeProvider.GetElapsedTime(startedTimestamp).TotalMilliseconds,
                2);
        }
    }
}
