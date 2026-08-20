using System.Globalization;
using DiscordBot.Jobs;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.UclFantasy;

[DisallowConcurrentExecution]
public sealed class UclDeadlineNotificationJob(
    IDiscordConnectionReadiness discordReadiness,
    UclFantasyDeadlineProvider deadlineProvider,
    IDiscordNotificationPublisher notificationPublisher,
    NotificationOptions notificationOptions,
    ReminderEligibilityService reminderEligibility,
    UclFantasyMessageCompositionService messageComposer,
    TimeProvider timeProvider,
    ILogger<UclDeadlineNotificationJob> logger) : IJob
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

                var deadline = await deadlineProvider.GetNextAsync(
                    context.CancellationToken);
                if (deadline is null)
                {
                    execution.SetEvent("NoUpcomingMatchday");
                    return new JobExecutionResult("SkippedNoUpcomingMatchday");
                }

                var matchdayIdentifier = deadline.RoundNumber.ToString(
                    CultureInfo.InvariantCulture);
                execution.SetEvent(matchdayIdentifier);
                var utcNow = timeProvider.GetUtcNow();
                var notificationType = reminderEligibility.SelectNotificationType(
                    utcNow,
                    deadline.DeadlineUtc,
                    NotificationTypes.UclDeadline24Hours,
                    NotificationTypes.UclDeadline1Hour);
                if (notificationType is null)
                {
                    return new JobExecutionResult("SkippedOutsideReminderWindow");
                }

                var message = messageComposer.ComposeDeadlineReminder(
                    deadline.DeadlineUtc,
                    utcNow);
                var deliveredCount = 0;
                var skippedCount = 0;
                var failedCount = 0;

                foreach (var target in notificationOptions.Targets)
                {
                    try
                    {
                        var delivered = await notificationPublisher.PublishOnceAsync(
                            target,
                            matchdayIdentifier,
                            notificationType,
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
                            "Failed to publish notification {NotificationType} for event {Event} to Discord " +
                            "guild {GuildId}, channel {ChannelId}, on job attempt {Attempt} with outcome {Outcome}",
                            notificationType,
                            matchdayIdentifier,
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
