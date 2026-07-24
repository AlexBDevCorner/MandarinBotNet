using System.Collections.Concurrent;

namespace DiscordBot.Notifications
{
    public sealed class NotificationDeliveryCoordinator(
        INotificationCheckpointStore checkpointStore)
    {
        private readonly ConcurrentDictionary<NotificationCheckpoint, SemaphoreSlim> _deliveryLocks = new();

        public async Task<bool> SendOnceAsync(
            NotificationCheckpoint checkpoint,
            Func<Task> sendAsync,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(checkpoint);
            ArgumentNullException.ThrowIfNull(sendAsync);

            var deliveryLock = _deliveryLocks.GetOrAdd(checkpoint, static _ => new SemaphoreSlim(1, 1));
            await deliveryLock.WaitAsync(cancellationToken);

            try
            {
                if (checkpointStore.IsDelivered(checkpoint))
                {
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();
                await sendAsync();
                checkpointStore.RecordDelivered(checkpoint, DateTimeOffset.UtcNow);
                return true;
            }
            finally
            {
                deliveryLock.Release();
            }
        }
    }
}
