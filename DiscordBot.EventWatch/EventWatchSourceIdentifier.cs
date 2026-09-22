namespace DiscordBot.EventWatch;

public static class EventWatchSourceIdentifier
{
    public static string Create(string watchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(watchId);
        return $"event-watch:{watchId.Trim()}";
    }
}
