using Discord.WebSocket;
using DiscordBot.Responses;
using DiscordBot.Services;
using Quartz;
using System.Text.Json;

namespace DiscordBot.Jobs
{
    public class PremierLeagueH2hStandingsInformationJob(DiscordSocketClient discordClient, 
        IHttpClientFactory httpClientFactory, 
        H2hMatchesPlayedService h2HMatchesPlayedService) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://fantasy.premierleague.com");
            var response = await client.GetAsync("/api/leagues-h2h/1671824/standings/");
            var jsonResponse = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(jsonResponse)) return;

            var standings = JsonSerializer.Deserialize<HeadToHeadStandingsResponse>(jsonResponse);

            if (standings == null ||
                standings.HeadToHeadStandings == null ||
                standings.HeadToHeadStandings.Results == null ||
                standings.HeadToHeadStandings.Results.Count == 0) return;

            if (standings.HeadToHeadStandings.Results[0].MatchesPlayed == h2HMatchesPlayedService.MatchesPlayed) return;

            foreach (var guild in discordClient.Guilds)
            {
                var generalChannel = guild.TextChannels
                    .FirstOrDefault(c => (c.Name.Equals("general", StringComparison.OrdinalIgnoreCase)
                        || c.Name.Equals("announcement-bot", StringComparison.OrdinalIgnoreCase)));

                if (generalChannel is null) continue;

                try
                {
                    await generalChannel.SendMessageAsync(GetEventSummary(standings.HeadToHeadStandings.Results));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send message");
                }
            }

            h2HMatchesPlayedService.MatchesPlayed = standings.HeadToHeadStandings.Results[0].MatchesPlayed;
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
            var summary = $"@everyone Лига Пельменных Обнимашек:";

            foreach (var result in results)
            {
                summary += $"\n{GetRankEmoji(result.Rank)} {result.EntryName} {result.Total}";
            }

            return summary;
        }
    }
}
