using DiscordBot.FantasyPremierLeague.PriceChanges;
using DiscordBot.Notifications;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.Jobs;

[DisallowConcurrentExecution]
public sealed class FplPriceChangeJob(
    IDiscordConnectionReadiness discordReadiness,
    FplPriceChangeService priceChangeService,
    FplLeaguePriceChangeService leaguePriceChangeService,
    FplPriceChangeMessageCompositionService messageComposer,
    IDiscordNotificationPublisher notificationPublisher,
    NotificationOptions notificationOptions,
    TimeProvider timeProvider,
    ILogger<FplPriceChangeJob> logger) : IJob
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

                var priceCheck = await priceChangeService.CheckAsync(
                    context.CancellationToken);
                if (priceCheck.Changes.Count == 0)
                {
                    execution.SetEvent("NoPriceChanges");
                    priceChangeService.SaveSnapshot(priceCheck);
                    return new JobExecutionResult("SkippedNoPriceChanges");
                }

                var sourceIdentifier = FplPriceChangeSourceIdentifier.Create(
                    priceCheck.PreviousSnapshotVersion,
                    priceCheck.Changes);
                execution.SetEvent(sourceIdentifier);
                var report = await leaguePriceChangeService.CreateReportAsync(
                    priceCheck.Changes,
                    priceCheck.CheckedAtUtc,
                    priceCheck.CurrentEventId,
                    context.CancellationToken);
                var message = messageComposer.Compose(report);
                var deliveredCount = 0;
                var skippedCount = 0;
                var failedCount = 0;

                foreach (var target in notificationOptions.Targets)
                {
                    try
                    {
                        var delivered = await notificationPublisher.PublishOnceAsync(
                            target,
                            sourceIdentifier,
                            NotificationTypes.FplPriceChanges,
                            message,
                            context.CancellationToken);
                        if (delivered)
                        {
                            deliveredCount++;
                        }
                        else
                        {
                            skippedCount++;
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
                            "Failed to publish notification {NotificationType} for event " +
                            "{Event} to Discord guild {GuildId}, channel {ChannelId}, " +
                            "on job attempt {Attempt} with outcome {Outcome}",
                            NotificationTypes.FplPriceChanges,
                            sourceIdentifier,
                            target.GuildId,
                            target.ChannelId,
                            execution.Attempt,
                            "Failed");
                    }
                }

                if (failedCount == 0 && skippedCount == 0)
                {
                    priceChangeService.SaveSnapshot(priceCheck);
                }

                return new JobExecutionResult(
                    failedCount == 0 ? "Completed" : "CompletedWithDeliveryFailures",
                    deliveredCount,
                    skippedCount,
                    failedCount);
            });
    }
}
