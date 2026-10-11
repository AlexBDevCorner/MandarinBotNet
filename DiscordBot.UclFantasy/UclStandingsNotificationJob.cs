using System.Globalization;
using DiscordBot.Jobs;
using DiscordBot.Notifications;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.UclFantasy;

[DisallowConcurrentExecution]
public sealed class UclStandingsNotificationJob(
    IDiscordConnectionReadiness discordReadiness,
    UclLeagueStandingsProvider standingsProvider,
    IDiscordNotificationPublisher notificationPublisher,
    INotificationCheckpointStore checkpointStore,
    IClassicStandingsPublicationStateStore publicationStateStore,
    UclFantasyOptions leagueOptions,
    NotificationOptions notificationOptions,
    UclStandingsSnapshotService snapshotService,
    UclFantasyMessageCompositionService messageComposer,
    TimeProvider timeProvider,
    ILogger<UclStandingsNotificationJob> logger) : IJob
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

                UclLeagueStandings standings;
                try
                {
                    standings = await standingsProvider.GetCurrentStandingsAsync(
                        context.CancellationToken);
                }
                catch (UclFantasyApiException exception)
                {
                    execution.SetEvent("SourceUnavailable");
                    logger.LogWarning(
                        exception,
                        "UCL standings poll failed with {FailureKind}; baseline unchanged.",
                        exception.FailureKind);
                    return new JobExecutionResult("SkippedSourceUnavailable");
                }

                if (standings.Members.Count == 0)
                {
                    execution.SetEvent("EmptyStandings");
                    logger.LogWarning(
                        "UCL standings poll returned no members for league {LeagueId}; baseline unchanged.",
                        leagueOptions.LeagueId);
                    return new JobExecutionResult("SkippedNoStandings");
                }

                var fingerprint = snapshotService.ComputeFingerprint(standings);
                execution.SetEvent(fingerprint);

                var message = messageComposer.ComposeStandingsUpdate(standings);
                var deliveredCount = 0;
                var skippedCount = 0;
                var failedCount = 0;
                var unchangedCount = 0;
                var initialCount = 0;

                foreach (var target in notificationOptions.Targets)
                {
                    var publicationState = publicationStateStore.GetPublicationState(
                        target.GuildId,
                        target.ChannelId,
                        leagueOptions.LeagueId);

                    if (publicationState is null)
                    {
                        publicationStateStore.SetPublicationState(
                            target.GuildId,
                            target.ChannelId,
                            leagueOptions.LeagueId,
                            fingerprint,
                            0,
                            timeProvider.GetUtcNow());
                        initialCount++;
                        skippedCount++;
                        logger.LogInformation(
                            "Recording initial UCL standings {NotificationType} baseline for event {Event} to Discord " +
                            "guild {GuildId}, channel {ChannelId}, on job attempt {Attempt} with outcome {Outcome}",
                            NotificationTypes.UclStandings,
                            fingerprint,
                            target.GuildId,
                            target.ChannelId,
                            execution.Attempt,
                            "SkippedInitialBaseline");
                        continue;
                    }

                    if (string.Equals(
                            publicationState.Fingerprint,
                            fingerprint,
                            StringComparison.Ordinal))
                    {
                        unchangedCount++;
                        skippedCount++;
                        logger.LogInformation(
                            "Skipping unchanged UCL standings {NotificationType} for event {Event} to Discord " +
                            "guild {GuildId}, channel {ChannelId}, on job attempt {Attempt} with outcome {Outcome}",
                            NotificationTypes.UclStandings,
                            fingerprint,
                            target.GuildId,
                            target.ChannelId,
                            execution.Attempt,
                            "SkippedUnchangedStandings");
                        continue;
                    }

                    var sequence = checked(publicationState.Sequence + 1);
                    var sourceIdentifier = string.Concat(
                        "ucl-standings:league-",
                        leagueOptions.LeagueId.ToString(CultureInfo.InvariantCulture),
                        ":seq-",
                        sequence.ToString(CultureInfo.InvariantCulture),
                        ":",
                        fingerprint);

                    try
                    {
                        var delivered = await notificationPublisher.PublishOnceAsync(
                            target,
                            sourceIdentifier,
                            NotificationTypes.UclStandings,
                            message,
                            context.CancellationToken);
                        var checkpoint = new NotificationCheckpoint(
                            target.GuildId,
                            target.ChannelId,
                            sourceIdentifier,
                            NotificationTypes.UclStandings);
                        var checkpointExists = delivered || checkpointStore.IsDelivered(checkpoint);
                        if (checkpointExists)
                        {
                            publicationStateStore.SetPublicationState(
                                target.GuildId,
                                target.ChannelId,
                                leagueOptions.LeagueId,
                                fingerprint,
                                sequence,
                                timeProvider.GetUtcNow());
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
                            NotificationTypes.UclStandings,
                            sourceIdentifier,
                            target.GuildId,
                            target.ChannelId,
                            execution.Attempt,
                            "Failed");
                    }
                }

                if (failedCount == 0 && deliveredCount == 0)
                {
                    if (initialCount > 0 && unchangedCount == 0)
                    {
                        return new JobExecutionResult(
                            "SkippedInitialBaseline",
                            deliveredCount,
                            skippedCount,
                            failedCount);
                    }

                    if (unchangedCount > 0 || initialCount > 0)
                    {
                        return new JobExecutionResult(
                            "SkippedUnchangedStandings",
                            deliveredCount,
                            skippedCount,
                            failedCount);
                    }

                    return new JobExecutionResult(
                        "SkippedNoTargets",
                        deliveredCount,
                        skippedCount,
                        failedCount);
                }

                return new JobExecutionResult(
                    failedCount == 0 ? "Completed" : "CompletedWithDeliveryFailures",
                    deliveredCount,
                    skippedCount,
                    failedCount);
            });
    }
}
