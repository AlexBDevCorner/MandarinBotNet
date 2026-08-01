using Discord.WebSocket;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.Notifications;
using DiscordBot.Responses;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Globalization;

namespace DiscordBot.Jobs
{
    [DisallowConcurrentExecution]
    public class PremierLeagueH2hStandingsInformationJob(
        IDiscordConnectionReadiness discordReadiness,
        IFantasyPremierLeagueClient premierLeagueClient,
        IDiscordNotificationPublisher notificationPublisher,
        FantasyPremierLeagueOptions leagueOptions,
        NotificationOptions notificationOptions,
        ILogger<PremierLeagueH2hStandingsInformationJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            await discordReadiness.WaitUntilReadyAsync(context.CancellationToken);

            var standings = await premierLeagueClient.GetHeadToHeadStandingsAsync(
                leagueOptions.HeadToHeadLeagueId,
                context.CancellationToken);

            if (standings.HeadToHeadStandings.Results.Count == 0) return;

            var results = standings.HeadToHeadStandings.Results;
            var message = GetEventSummary(results);
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

        private static string GetRankEmoji(int rank) => rank switch
        {
            1 => ":one:",
            2 => ":two:",
            3 => ":three:",
            _ => ":four:"
        };

        private static string GetEventSummary(List<HeadToHeadStanding> results)
        {
            var summary = "Лига Пельменных Обнимашек-К-Обнимашкам:";

            foreach (var result in results)
            {
                summary += $"\n{GetRankEmoji(result.Rank)} {result.EntryName} {result.Total}";
            }

            return summary;
        }
    }
}
