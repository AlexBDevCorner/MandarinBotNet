using System.Globalization;
using DiscordBot.FantasyPremierLeague.Recap;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.Jobs;

[DisallowConcurrentExecution]
public sealed class FplGameweekRecapJob(
    IDiscordConnectionReadiness discordReadiness,
    FplGameweekRecapService recapService,
    PremierLeagueMessageCompositionService messageComposer,
    IDiscordNotificationPublisher notificationPublisher,
    NotificationOptions notificationOptions,
    TimeProvider timeProvider,
    ILogger<FplGameweekRecapJob> logger) : IJob
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

                var recap = await recapService.GetLatestAsync(
                    context.CancellationToken);
                if (recap is null)
                {
                    execution.SetEvent("NoCompleteSnapshot");
                    return new JobExecutionResult("SkippedNoCompleteSnapshot");
                }

                var sourceIdentifier =
                    $"{recap.Season}-event-{recap.EventId.ToString(CultureInfo.InvariantCulture)}";
                execution.SetEvent(sourceIdentifier);
                var message = messageComposer.ComposeGameweekRecap(recap);
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
                            NotificationTypes.FplGameweekRecap,
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
                            "Failed to publish notification {NotificationType} for event {Event} " +
                            "to Discord guild {GuildId}, channel {ChannelId}, on job attempt " +
                            "{Attempt} with outcome {Outcome}",
                            NotificationTypes.FplGameweekRecap,
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
