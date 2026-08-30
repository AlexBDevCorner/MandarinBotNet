using DiscordBot.FantasyPremierLeague.Live;
using DiscordBot.Notifications;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.Jobs;

[DisallowConcurrentExecution]
public sealed class FplLiveInsightsJob(
    IDiscordConnectionReadiness discordReadiness,
    FplLiveInsightsService liveInsightsService,
    FplLiveNotificationService liveNotificationService,
    FplLiveHighlightMessageComposer messageComposer,
    IDiscordNotificationPublisher notificationPublisher,
    INotificationCheckpointStore checkpointStore,
    NotificationOptions notificationOptions,
    TimeProvider timeProvider,
    ILogger<FplLiveInsightsJob> logger) : IJob
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

                var result = await liveInsightsService.GetCurrentAsync(
                    context.CancellationToken);
                switch (result.Availability)
                {
                    case FplLiveInsightsAvailability.NoActiveGameweek:
                        execution.SetEvent("NoActiveGameweek");
                        return new JobExecutionResult("SkippedNoActiveGameweek");
                    case FplLiveInsightsAvailability.Unavailable:
                        execution.SetEvent("SourceUnavailable");
                        return new JobExecutionResult("SkippedSourceUnavailable");
                    case FplLiveInsightsAvailability.Available:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var gameweek = result.Gameweek ?? throw new InvalidDataException(
                    "An available FPL live insights result did not include a gameweek.");
                var evaluation = liveNotificationService.Observe(
                    gameweek,
                    notificationOptions.Targets);
                execution.SetEvent(evaluation.Outcome.ToString());
                var deliveredCount = 0;
                var skippedCount = 0;
                var failedCount = 0;

                foreach (var digest in evaluation.Digests)
                {
                    var target = notificationOptions.Targets.Single(configuredTarget =>
                        configuredTarget.GuildId == digest.GuildId &&
                        configuredTarget.ChannelId == digest.ChannelId);
                    var message = messageComposer.Compose(digest);

                    try
                    {
                        var delivered = await notificationPublisher.PublishOnceAsync(
                            target,
                            digest.SourceIdentifier,
                            NotificationTypes.FplLiveInsights,
                            message,
                            context.CancellationToken);
                        var checkpointExists = delivered || checkpointStore.IsDelivered(
                            new NotificationCheckpoint(
                                target.GuildId,
                                target.ChannelId,
                                digest.SourceIdentifier,
                                NotificationTypes.FplLiveInsights));
                        if (checkpointExists)
                        {
                            liveNotificationService.MarkPublished(digest);
                        }

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
                            "{Event} to Discord guild {GuildId}, channel {ChannelId}, on " +
                            "job attempt {Attempt} with outcome {Outcome}",
                            NotificationTypes.FplLiveInsights,
                            digest.SourceIdentifier,
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
