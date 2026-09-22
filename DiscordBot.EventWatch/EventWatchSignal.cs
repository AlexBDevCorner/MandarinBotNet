namespace DiscordBot.EventWatch;

public sealed record EventWatchSignal(
    EventWatchSignalKind Kind,
    string WatchId,
    string WatchTitle,
    string SourceUrl,
    string? TicketUrl,
    string Evidence);
