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
    FantasyPremierLeagueOptions leagueOptions,
    NotificationOptions notificationOptions,
    StandingsPublicationEligibilityService publicationEligibility,
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
                var message = messageComposer.ComposeClassicStandings(
                    results,
                    winnerSelection.SelectEventWinners(results),
                    standingsChangeService.GetChanges(results),
                    Random.Shared.Next(
                        PremierLeagueMessageCompositionService.ClassicCongratulationsVariantCount));
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
                            NotificationTypes.ClassicStandings,
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
                            NotificationTypes.ClassicStandings,
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
