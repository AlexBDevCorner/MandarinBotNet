using System.Globalization;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.Jobs;

[DisallowConcurrentExecution]
public sealed class PremierLeagueNotificationJob(
    IDiscordConnectionReadiness discordReadiness,
    IFantasyPremierLeagueClient premierLeagueClient,
    IDiscordNotificationPublisher notificationPublisher,
    NotificationOptions notificationOptions,
    DeadlineSelectionService deadlineSelection,
    ReminderEligibilityService reminderEligibility,
    PremierLeagueMessageCompositionService messageComposer,
    TimeProvider timeProvider,
    ILogger<PremierLeagueNotificationJob> logger) : IJob
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

                var bootstrap = await premierLeagueClient.GetBootstrapStaticAsync(
                    context.CancellationToken);
                var deadline = deadlineSelection.SelectNext(bootstrap.Events);
                if (deadline is null)
                {
                    execution.SetEvent("NoUpcomingEvent");
                    return new JobExecutionResult("SkippedNoUpcomingEvent");
                }

                var eventIdentifier = deadline.EventId.ToString(
                    CultureInfo.InvariantCulture);
                execution.SetEvent(eventIdentifier);
                var utcNow = timeProvider.GetUtcNow();
                var notificationType = reminderEligibility.SelectNotificationType(
                    utcNow,
                    deadline.DeadlineUtc);
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
                            eventIdentifier,
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
                            eventIdentifier,
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
