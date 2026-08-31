using DiscordBot.FantasyPremierLeague.ChipWatch;
using DiscordBot.Jobs;
using DiscordBot.Notifications;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.Jobs;

[DisallowConcurrentExecution]
public sealed class FplChipWatchJob(
    IDiscordConnectionReadiness discordReadiness,
    FplChipWatchService chipWatchService,
    FplChipWatchNotificationEligibilityService eligibilityService,
    FplChipWatchMessageCompositionService messageComposer,
    IDiscordNotificationPublisher notificationPublisher,
    NotificationOptions notificationOptions,
    FantasyPremierLeagueOptions fantasyOptions,
    TimeProvider timeProvider,
    ILogger<FplChipWatchJob> logger) : IJob
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

                var report = await chipWatchService.CreateReportAsync(context.CancellationToken);
                if (report is null)
                {
                    execution.SetEvent("SkippedNoUpcomingEvent");
                    return new JobExecutionResult("SkippedNoUpcomingEvent");
                }

                execution.SetEvent(FplChipWatchSourceIdentifier.Create(report.TargetEventId));

                var now = timeProvider.GetUtcNow();
                var withinWindow = eligibilityService.IsWithinWindow(now, report.DeadlineUtc, fantasyOptions.ChipWatch);
                if (!withinWindow)
                {
                    logger.LogInformation(
                        "Chip Watch job outside window: now {Now} deadline {Deadline} window {Start}-{End}.",
                        now,
                        report.DeadlineUtc,
                        fantasyOptions.ChipWatch.NotificationWindowStartHours,
                        fantasyOptions.ChipWatch.NotificationWindowEndHours);
                    return new JobExecutionResult("SkippedOutsideWindow");
                }

                var hasSignals = eligibilityService.HasMeaningfulSignals(report, fantasyOptions.ChipWatch);
                if (!hasSignals)
                {
                    return new JobExecutionResult("SkippedNoChipWatchSignals");
                }

                var message = messageComposer.ComposeScheduledDigest(report);
                var sourceIdentifier = FplChipWatchSourceIdentifier.Create(report.TargetEventId);

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
                            NotificationTypes.FplChipWatch,
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
                    catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        failedCount++;
                        logger.LogError(
                            exception,
                            "Failed to publish notification {NotificationType} for event {Event} to Discord guild {GuildId}, channel {ChannelId}, on job attempt {Attempt} with outcome {Outcome}",
                            NotificationTypes.FplChipWatch,
                            sourceIdentifier,
                            target.GuildId,
                            target.ChannelId,
                            execution.Attempt,
                            "Failed");
                    }
                }

                return new JobExecutionResult(
                    failedCount == 0 ? "Completed" : "CompletedWithDeliveryFailures",
                    deliveredCount,
                    skippedCount,
                    failedCount);
            });
    }
}
