using DiscordBot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.EventWatch;

public sealed record EventWatchCheckResult(
    EventWatchDefinition Watch,
    IReadOnlyList<EventWatchSignal> Signals);

public sealed class EventWatchService(
    IEventWatchSource source,
    EventWatchSignalDetector detector,
    IOptions<MandarinBotOptions> options,
    ILogger<EventWatchService> logger)
{
    public async Task<IReadOnlyList<EventWatchCheckResult>> CheckAsync(
        CancellationToken cancellationToken)
    {
        var watches = options.Value.EventWatch.Watches
            .Where(watch => watch.Enabled)
            .ToList();

        if (watches.Count == 0)
        {
            return [];
        }

        var observations = await source.CollectAsync(cancellationToken);
        logger.LogInformation(
            "EventWatch collected {ObservationCount} ticket-catalogue products from source {SourceName}.",
            observations.Count,
            source.SourceName);

        var results = new List<EventWatchCheckResult>(watches.Count);
        foreach (var watch in watches)
        {
            var signals = detector.Detect(watch, observations);
            logger.LogInformation(
                "EventWatch watch {WatchId} detected {TicketAvailableCount} ticket-available signals from {ObservationCount} catalogue products.",
                watch.Id,
                signals.Count,
                observations.Count);
            results.Add(new EventWatchCheckResult(watch, signals));
        }

        return results;
    }
}
