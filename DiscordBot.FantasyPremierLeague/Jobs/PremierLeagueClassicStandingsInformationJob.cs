using DiscordBot.FantasyPremierLeague;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.Jobs;

[DisallowConcurrentExecution]
public sealed class PremierLeagueClassicStandingsInformationJob(
    IDiscordConnectionReadiness discordReadiness,
    IFantasyPremierLeagueClient premierLeagueClient,
    IDiscordNotificationPublisher notificationPublisher,
    INotificationCheckpointStore checkpointStore,
    IClassicStandingsPublicationStateStore publicationStateStore,
    FantasyPremierLeagueOptions leagueOptions,
    NotificationOptions notificationOptions,
    StandingsPublicationEligibilityService publicationEligibility,
    ClassicStandingsSnapshotService snapshotService,
    StandingsChangeService standingsChangeService,
    WinnerSelectionService winnerSelection,
    PremierLeagueMessageCompositionService messageComposer,
    TimeProvider timeProvider,
    ILogger<PremierLeagueClassicStandingsInformationJob> logger) : IJob
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

                var standings = await premierLeagueClient.GetClassicStandingsAsync(
                    leagueOptions.ClassicLeagueId,
                    context.CancellationToken);
                var sourceIdentifier = standings.LastUpdatedData
                    .ToUniversalTime()
                    .ToString("O");
                execution.SetEvent(sourceIdentifier);
                if (!publicationEligibility.IsUpdatedSinceYesterday(
                    standings.LastUpdatedData,
                    timeProvider.GetUtcNow()))
                {
                    return new JobExecutionResult("SkippedStandingsNotUpdated");
                }

                var results = standings.Standings.Results;
                if (results.Count == 0)
                {
                    return new JobExecutionResult("SkippedNoStandings");
                }

                // Compute the semantic change identity before composing or
                // randomizing any text. Timestamps, scheduling time and the
                // congratulations variant must not influence this comparison.
                var fingerprint = snapshotService.ComputeFingerprint(
                    leagueOptions.ClassicLeagueId,
                    results);
                execution.SetEvent(fingerprint);

                var message = messageComposer.ComposeClassicStandings(
                    results,
                    winnerSelection.SelectEventWinners(results),
                    standingsChangeService.GetChanges(results),
                    Random.Shared.Next(
                        PremierLeagueMessageCompositionService.ClassicCongratulationsVariantCount));
                var deliveredCount = 0;
                var skippedCount = 0;
                var failedCount = 0;
                var unchangedCount = 0;

                foreach (var target in notificationOptions.Targets)
                {
                    // Each guild/channel keeps its own last-delivered snapshot.
                    // A successful send to one target must not silence the same
                    // update for another target.
                    var lastFingerprint = publicationStateStore.GetLastPublishedFingerprint(
                        target.GuildId,
                        target.ChannelId,
                        leagueOptions.ClassicLeagueId);
                    if (string.Equals(
                            lastFingerprint,
                            fingerprint,
                            StringComparison.Ordinal))
                    {
                        unchangedCount++;
                        skippedCount++;
                        logger.LogInformation(
                            "Skipping unchanged classic standings {NotificationType} for event {Event} to Discord " +
                            "guild {GuildId}, channel {ChannelId}, on job attempt {Attempt} with outcome {Outcome}",
                            NotificationTypes.ClassicStandings,
                            fingerprint,
                            target.GuildId,
                            target.ChannelId,
                            execution.Attempt,
                            "SkippedUnchangedStandings");
                        continue;
                    }

                    try
                    {
                        var delivered = await notificationPublisher.PublishOnceAsync(
                            target,
                            sourceIdentifier,
                            NotificationTypes.ClassicStandings,
                            message,
                            context.CancellationToken);
                        var checkpoint = new NotificationCheckpoint(
                            target.GuildId,
                            target.ChannelId,
                            sourceIdentifier,
                            NotificationTypes.ClassicStandings);
                        var checkpointExists = delivered || checkpointStore.IsDelivered(checkpoint);
                        if (checkpointExists)
                        {
                            // Advance the baseline only after actual successful
                            // delivery (or a previous delivery of the same
                            // source). Failures and unavailable targets keep
                            // the previous baseline so the update stays
                            // eligible for retry.
                            publicationStateStore.SetLastPublishedFingerprint(
                                target.GuildId,
                                target.ChannelId,
                                leagueOptions.ClassicLeagueId,
                                fingerprint,
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
                            NotificationTypes.ClassicStandings,
                            sourceIdentifier,
                            target.GuildId,
                            target.ChannelId,
                            execution.Attempt,
                            "Failed");
                    }
                }

                if (failedCount == 0 && deliveredCount == 0 && unchangedCount > 0 &&
                    unchangedCount == notificationOptions.Targets.Count && notificationOptions.Targets.Count > 0)
                {
                    return new JobExecutionResult(
                        "SkippedUnchangedStandings",
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
