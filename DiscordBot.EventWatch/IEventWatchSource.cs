namespace DiscordBot.EventWatch;

public interface IEventWatchSource
{
    string SourceName { get; }

    Task<IReadOnlyList<EventWatchObservation>> CollectAsync(
        CancellationToken cancellationToken);
}
