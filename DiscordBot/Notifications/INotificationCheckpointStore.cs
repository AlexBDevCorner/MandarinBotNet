namespace DiscordBot.Notifications
{
    public interface INotificationCheckpointStore
    {
        bool IsDelivered(NotificationCheckpoint checkpoint);

        void RecordDelivered(NotificationCheckpoint checkpoint, DateTimeOffset deliveredAtUtc);
    }
}
