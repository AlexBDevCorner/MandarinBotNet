using DiscordBot.Jobs;
using DiscordBot.Notifications;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.EventWatch;

[DisallowConcurrentExecution]
public sealed class EventWatchJob(
    IDiscordConnectionReadiness discordReadiness,
    EventWatchService watchService,
    EventWatchMessageCompositionService messageComposer,
    IDiscordNotificationPublisher notificationPublisher,
    TimeProvider timeProvider,
    ILogger<EventWatchJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        return JobExecutionLogging.RunAsync(
            context,
            timeProvider,
            logger,
            async execution =>
            {
                await discordReadiness.WaitUntilReadyAsync(context.CancellationToken);

                IReadOnlyList<EventWatchCheckResult> results;
                try
                {
                    results = await watchService.CheckAsync(context.CancellationToken);
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "EventWatch collection failed with outcome {Outcome}.",
                        "CollectionFailed");
                    throw;
                }

                if (results.Count == 0)
                {
                    execution.SetEvent("SkippedNoEnabledWatches");
                    return new JobExecutionResult("SkippedNoEnabledWatches");
                }

                var deliveredCount = 0;
                var skippedCount = 0;
                var failedCount = 0;
                var signalSummary = string.Join(
                    ", ",
                    results.Select(result =>
                        $"{result.Watch.Id}:{result.Signals.Count}"));

                execution.SetEvent(
                    results.Count == 1
                        ? results[0].Watch.Id
                        : $"watches-{results.Count}");

                foreach (var result in results)
                {
                    var watch = result.Watch;
                    logger.LogInformation(
                        "EventWatch watch {WatchId} evaluated {SignalCount} signals from {SourceCount} sources.",
                        watch.Id,
                        result.Signals.Count,
                        "riga-fc");

                    foreach (var signal in result.Signals)
                    {
                        var notificationType = signal.Kind switch
                        {
                            EventWatchSignalKind.Announcement =>
                                NotificationTypes.EventWatchAnnouncement,
                            EventWatchSignalKind.TicketLinkAvailable =>
                                NotificationTypes.EventWatchTicketLink,
                            _ => NotificationTypes.EventWatchAnnouncement
                        };
                        var sourceIdentifier = EventWatchSourceIdentifier.Create(watch.Id);
                        var message = messageComposer.Compose(signal);

                        foreach (var target in watch.Targets)
                        {
                            try
                            {
                                var delivered = await notificationPublisher.PublishOnceAsync(
                                    target,
                                    sourceIdentifier,
                                    notificationType,
                                    message,
                                    context.CancellationToken);
                                if (delivered)
                                {
                                    deliveredCount++;
                                    logger.LogInformation(
                                        "EventWatch watch {WatchId} signal {SignalKind} delivered to guild {GuildId}, channel {ChannelId} with outcome {Outcome}.",
                                        watch.Id,
                                        signal.Kind,
                                        target.GuildId,
                                        target.ChannelId,
                                        "Delivered");
                                }
                                else
                                {
                                    skippedCount++;
                                    logger.LogInformation(
                                        "EventWatch watch {WatchId} signal {SignalKind} skipped for guild {GuildId}, channel {ChannelId} with outcome {Outcome}.",
                                        watch.Id,
                                        signal.Kind,
                                        target.GuildId,
                                        target.ChannelId,
                                        "SkippedAlreadyDelivered");
                                }
                            }
                            catch (OperationCanceledException) when (
                                context.CancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception exception)
                            {
                                failedCount++;
                                logger.LogError(
                                    exception,
                                    "Failed to publish EventWatch notification {NotificationType} for watch {WatchId} to Discord guild {GuildId}, channel {ChannelId}, on job attempt {Attempt} with outcome {Outcome}",
                                    notificationType,
                                    watch.Id,
                                    target.GuildId,
                                    target.ChannelId,
                                    execution.Attempt,
                                    "Failed");
                            }
                        }
                    }

                    if (result.Signals.Count == 0)
                    {
                        logger.LogInformation(
                            "EventWatch watch {WatchId} found no qualifying signals with outcome {Outcome}.",
                            watch.Id,
                            "NoSignals");
                    }
                }

                logger.LogInformation(
                    "EventWatch job evaluated {WatchCount} watches ({SignalSummary}) with delivery outcome delivered {DeliveredCount}, skipped {SkippedCount}, failed {FailedCount}.",
                    results.Count,
                    signalSummary,
                    deliveredCount,
                    skippedCount,
                    failedCount);

                return new JobExecutionResult(
                    failedCount == 0 ? "Completed" : "CompletedWithDeliveryFailures",
                    deliveredCount,
                    skippedCount,
                    failedCount);
            });
    }
}
