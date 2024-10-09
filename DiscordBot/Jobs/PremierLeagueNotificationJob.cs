using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Text.Json;

namespace DiscordBot.Jobs
{
    public class PremierLeagueNotificationJob(DiscordSocketClient discordClient, IHttpClientFactory httpClientFactory, ILogger<PremierLeagueNotificationJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://fantasy.premierleague.com");
            var response = await client.GetStreamAsync("/api/bootstrap-static");
            using JsonDocument jsonDoc = await JsonDocument.ParseAsync(response);

            if (jsonDoc.RootElement.TryGetProperty("events", out JsonElement dataArrayElement))
            {
                foreach (JsonElement itemElement in dataArrayElement.EnumerateArray())
                {
                    itemElement.TryGetProperty("is_next", out JsonElement isNext);
                    var isNextText = isNext.ToString();
                    
                    if (isNext.ToString() == "True")
                    {
                        Console.WriteLine(isNextText);
                    }  
                }
            }
            logger.LogInformation($"Guild amount: {discordClient.Guilds.Count}");
            foreach (var guild in discordClient.Guilds)
            {
                var generalChannel = guild.TextChannels.FirstOrDefault(channel => channel.Name.Equals("general", StringComparison.OrdinalIgnoreCase));

                if (generalChannel is not null)
                {
                    try
                    {
                        await generalChannel.SendMessageAsync("Тестовое сообщение, всех люблю и обнимаю :people_hugging:");
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"Failed to send message to guild {guild.Name} ({guild.Id}): {ex.Message}");
                    }
                }
            }
        }
    }
}
