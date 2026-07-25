using Discord.WebSocket;
using DiscordBot.Notifications;
using DiscordBot.Responses;
using Quartz;
using System.Globalization;
using System.Text.Json;

namespace DiscordBot.Jobs
{
    [DisallowConcurrentExecution]
    public class PremierLeagueH2hStandingsInformationJob(
        DiscordSocketClient discordClient,
        IDiscordConnectionReadiness discordReadiness,
        IHttpClientFactory httpClientFactory,
        NotificationDeliveryCoordinator deliveryCoordinator) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            await discordReadiness.WaitUntilReadyAsync(context.CancellationToken);

            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://fantasy.premierleague.com");
            var response = await client.GetAsync(
                "/api/leagues-h2h/1671824/standings/",
                context.CancellationToken);
            var jsonResponse = await response.Content.ReadAsStringAsync(context.CancellationToken);

            if (string.IsNullOrEmpty(jsonResponse)) return;

            var standings = JsonSerializer.Deserialize<HeadToHeadStandingsResponse>(jsonResponse);

            if (standings == null ||
                standings.HeadToHeadStandings == null ||
                standings.HeadToHeadStandings.Results == null ||
                standings.HeadToHeadStandings.Results.Count == 0) return;

            foreach (var guild in discordClient.Guilds)
            {
                var generalChannel = guild.TextChannels
                    .FirstOrDefault(c => (c.Name.Equals("general", StringComparison.OrdinalIgnoreCase)
                        || c.Name.Equals("announcement-bot", StringComparison.OrdinalIgnoreCase)));

                if (generalChannel is null) continue;

                try
                {
                    var checkpoint = new NotificationCheckpoint(
                        guild.Id,
                        generalChannel.Id,
                        standings.HeadToHeadStandings.Results[0].MatchesPlayed.ToString(CultureInfo.InvariantCulture),
                        NotificationTypes.HeadToHeadStandings);

                    await deliveryCoordinator.SendOnceAsync(
                        checkpoint,
                        () => generalChannel.SendMessageAsync(GetEventSummary(standings.HeadToHeadStandings.Results)),
                        context.CancellationToken);
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    Console.WriteLine($"Failed to send message");
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
            var summary = $"@everyone Лига Пельменных Обнимашек-К-Обнимашкам:";

            foreach (var result in results)
            {
                summary += $"\n{GetRankEmoji(result.Rank)} {result.EntryName} {result.Total}";
            }

            return summary;
        }
    }
}
