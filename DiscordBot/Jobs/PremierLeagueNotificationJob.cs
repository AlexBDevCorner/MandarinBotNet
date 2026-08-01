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
    public async Task Execute(IJobExecutionContext context)
    {
        await discordReadiness.WaitUntilReadyAsync(context.CancellationToken);

        var bootstrap = await premierLeagueClient.GetBootstrapStaticAsync(
            context.CancellationToken);
        var deadline = deadlineSelection.SelectNext(bootstrap.Events);
        if (deadline is null)
        {
            return;
        }

        var utcNow = timeProvider.GetUtcNow();
        var notificationType = reminderEligibility.SelectNotificationType(
            utcNow,
            deadline.DeadlineUtc);
        if (notificationType is null)
        {
            return;
        }

        var message = messageComposer.ComposeDeadlineReminder(
            deadline.DeadlineUtc,
            utcNow);

        foreach (var target in notificationOptions.Targets)
        {
            try
            {
                await notificationPublisher.PublishOnceAsync(
                    target,
                    deadline.EventId.ToString(CultureInfo.InvariantCulture),
                    notificationType,
                    message,
                    context.CancellationToken);
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to publish deadline notification to Discord guild {GuildId}, channel {ChannelId}.",
                    target.GuildId,
                    target.ChannelId);
            }
        }
    }
}
