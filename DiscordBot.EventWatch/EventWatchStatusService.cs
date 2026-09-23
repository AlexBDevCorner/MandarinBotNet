using DiscordBot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.EventWatch;

public sealed class EventWatchStatusService(
    IEventWatchSource source,
    EventWatchSignalDetector detector,
    IOptions<MandarinBotOptions> options,
    ILogger<EventWatchStatusService> logger)
{
    public async Task<EventWatchStatusReport> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var schedulingEnabled = options.Value.Schedules.EventWatch.Enabled;
        var watches = options.Value.EventWatch.Watches
            .Where(static watch => watch.Enabled)
            .ToList();
        var sourcePages = RigaFcClient.PageUris
            .Select(static uri => uri.ToString())
            .ToList();
        var enabledSummaries = watches
            .Select(static watch => new EventWatchEnabledWatchSummary(
                watch.Id,
                watch.Title))
            .ToList();

        IReadOnlyList<EventWatchObservation> observations;
        try
        {
            observations = await source.CollectAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "EventWatch status collection failed with outcome {Outcome}.",
                "CollectionFailed");
            return new EventWatchStatusReport(
                schedulingEnabled,
                enabledSummaries,
                sourcePages,
                CollectionSucceeded: false,
                FailureReason: exception.Message,
                ObservationCount: 0,
                WatchResults: []);
        }

        var results = new List<EventWatchPerWatchStatus>(watches.Count);
        foreach (var watch in watches)
        {
            var signals = detector.Detect(watch, observations);
            var evidenceUrls = signals
                .Select(static signal => signal.SourceUrl)
                .Where(static url => !string.IsNullOrWhiteSpace(url))
                .Distinct(StringComparer.Ordinal)
                .Take(3)
                .ToList();

            logger.LogInformation(
                "EventWatch status evaluated watch {WatchId} with {TicketAvailableCount} ticket-available signals from {ObservationCount} catalogue products.",
                watch.Id,
                signals.Count,
                observations.Count);

            results.Add(new EventWatchPerWatchStatus(
                watch.Id,
                watch.Title,
                watch.MatchTerms
                    .Where(static term => !string.IsNullOrWhiteSpace(term))
                    .Select(static term => term.Trim())
                    .ToList(),
                signals.Count,
                evidenceUrls));
        }

        logger.LogInformation(
            "EventWatch status collected {ObservationCount} catalogue products for {WatchCount} enabled watches with outcome {Outcome}.",
            observations.Count,
            watches.Count,
            "Succeeded");

        return new EventWatchStatusReport(
            schedulingEnabled,
            enabledSummaries,
            sourcePages,
            CollectionSucceeded: true,
            FailureReason: null,
            ObservationCount: observations.Count,
            WatchResults: results);
    }
}
