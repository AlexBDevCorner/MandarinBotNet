namespace DiscordBot.EventWatch;

public sealed record EventWatchEnabledWatchSummary(
    string Id,
    string Title);

public sealed record EventWatchPerWatchStatus(
    string WatchId,
    string WatchTitle,
    IReadOnlyList<string> MatchTerms,
    int TicketAvailableCount,
    IReadOnlyList<string> EvidenceSourceUrls);

public sealed record EventWatchStatusReport(
    bool SchedulingEnabled,
    IReadOnlyList<EventWatchEnabledWatchSummary> EnabledWatches,
    IReadOnlyList<string> SourcePages,
    bool CollectionSucceeded,
    string? FailureReason,
    int ObservationCount,
    IReadOnlyList<EventWatchPerWatchStatus> WatchResults)
{
    public bool HasSignals =>
        WatchResults.Any(static result => result.TicketAvailableCount > 0);
}
