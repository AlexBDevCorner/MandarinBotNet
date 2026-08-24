namespace DiscordBot.Notifications
{
    public sealed record NotificationCheckpoint(
        ulong GuildId,
        ulong ChannelId,
        string SourceIdentifier,
        string NotificationType);
}
