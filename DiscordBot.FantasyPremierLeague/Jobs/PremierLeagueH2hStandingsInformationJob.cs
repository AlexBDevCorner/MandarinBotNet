using System.Globalization;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.Jobs;

[DisallowConcurrentExecution]
public sealed class PremierLeagueH2hStandingsInformationJob(
    IDiscordConnectionReadiness discordReadiness,
    IFantasyPremierLeagueClient premierLeagueClient,
    IDiscordNotificationPublisher notificationPublisher,
    FantasyPremierLeagueOptions leagueOptions,
    NotificationOptions notificationOptions,
    PremierLeagueMessageCompositionService messageComposer,
    TimeProvider timeProvider,
    ILogger<PremierLeagueH2hStandingsInformationJob> logger) : IJob
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

                var standings = await premierLeagueClient.GetHeadToHeadStandingsAsync(
                    leagueOptions.HeadToHeadLeagueId,
                    context.CancellationToken);
                var results = standings.HeadToHeadStandings.Results;
                if (results.Count == 0)
                {
                    execution.SetEvent("NoMatches");
                    return new JobExecutionResult("SkippedNoMatches");
                }

                var message = messageComposer.ComposeHeadToHeadStandings(results);
                var sourceIdentifier = results[0].MatchesPlayed
                    .ToString(CultureInfo.InvariantCulture);
                execution.SetEvent(sourceIdentifier);
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
                            NotificationTypes.HeadToHeadStandings,
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
                            NotificationTypes.HeadToHeadStandings,
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
