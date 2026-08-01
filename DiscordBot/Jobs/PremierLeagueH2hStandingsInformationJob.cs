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
    ILogger<PremierLeagueH2hStandingsInformationJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await discordReadiness.WaitUntilReadyAsync(context.CancellationToken);

        var standings = await premierLeagueClient.GetHeadToHeadStandingsAsync(
            leagueOptions.HeadToHeadLeagueId,
            context.CancellationToken);
        var results = standings.HeadToHeadStandings.Results;
        if (results.Count == 0)
        {
            return;
        }

        var message = messageComposer.ComposeHeadToHeadStandings(results);
        var sourceIdentifier = results[0].MatchesPlayed
            .ToString(CultureInfo.InvariantCulture);

        foreach (var target in notificationOptions.Targets)
        {
            try
            {
                await notificationPublisher.PublishOnceAsync(
                    target,
                    sourceIdentifier,
                    NotificationTypes.HeadToHeadStandings,
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
                    "Failed to publish head-to-head standings to Discord guild {GuildId}, channel {ChannelId}.",
                    target.GuildId,
                    target.ChannelId);
            }
        }
    }
}
