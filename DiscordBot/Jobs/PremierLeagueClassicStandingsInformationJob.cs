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
    public async Task Execute(IJobExecutionContext context)
    {
        await discordReadiness.WaitUntilReadyAsync(context.CancellationToken);

        var standings = await premierLeagueClient.GetClassicStandingsAsync(
            leagueOptions.ClassicLeagueId,
            context.CancellationToken);
        if (!publicationEligibility.IsUpdatedSinceYesterday(
            standings.LastUpdatedData,
            timeProvider.GetUtcNow()))
        {
            return;
        }

        var results = standings.Standings.Results;
        var message = messageComposer.ComposeClassicStandings(
            results,
            winnerSelection.SelectEventWinner(results),
            standingsChangeService.GetChanges(results),
            Random.Shared.Next(
                PremierLeagueMessageCompositionService.ClassicCongratulationsVariantCount));
        var sourceIdentifier = standings.LastUpdatedData
            .ToUniversalTime()
            .ToString("O");

        foreach (var target in notificationOptions.Targets)
        {
            try
            {
                await notificationPublisher.PublishOnceAsync(
                    target,
                    sourceIdentifier,
                    NotificationTypes.ClassicStandings,
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
                    "Failed to publish classic standings to Discord guild {GuildId}, channel {ChannelId}.",
                    target.GuildId,
                    target.ChannelId);
            }
        }
    }
}
