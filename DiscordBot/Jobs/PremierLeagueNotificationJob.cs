using Discord.WebSocket;
using Quartz;
using System.Globalization;
using System.Text.Json;

namespace DiscordBot.Jobs
{
    public class PremierLeagueNotificationJob(DiscordSocketClient discordClient, IHttpClientFactory httpClientFactory) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://fantasy.premierleague.com");
            var response = await client.GetStreamAsync("/api/bootstrap-static");
            using JsonDocument jsonDoc = await JsonDocument.ParseAsync(response);
            DateTime? deadline = null;
            TimeZoneInfo gmtPlus3Zone = TimeZoneInfo.CreateCustomTimeZone("GMT+3", TimeSpan.FromHours(3), "GMT+3", "GMT+3");

            if (jsonDoc.RootElement.TryGetProperty("events", out JsonElement dataArrayElement))
            {
                foreach (JsonElement itemElement in dataArrayElement.EnumerateArray())
                {
                    var isNext = itemElement.GetProperty("is_next").ToString();

                    if (isNext is null) continue;

                    if (isNext.ToString() == "True")
                    {
                        var epochTime = itemElement.GetProperty("deadline_time_epoch").GetInt64();
                        var dateTimeUtc = DateTimeOffset.FromUnixTimeSeconds(epochTime).UtcDateTime;

                        deadline = TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, gmtPlus3Zone);
                    }
                }
            }

            if (deadline is null) return;
            
            foreach (var guild in discordClient.Guilds)
            {
                var generalChannel = guild.TextChannels
                    .FirstOrDefault(c => c.Name.Equals("general", StringComparison.OrdinalIgnoreCase)
                        || c.Name.Equals("основной", StringComparison.OrdinalIgnoreCase));

                if (generalChannel is null) continue;

                try
                {
                    var castedDeadline = (DateTime)deadline;
                    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, gmtPlus3Zone);
                    var difference = castedDeadline.Subtract(now);

                    var russianCulture = new CultureInfo("ru-RU");
                    
                    await generalChannel.SendMessageAsync($"@everyone Привет мои любители АПЛ и обнимашек! :people_hugging: Следующий тур уже скоро -" +
                        $" {castedDeadline.ToString("dd MMMM yyyy, HH:mm", russianCulture)}, это {castedDeadline.ToString("dddd", russianCulture)}" +
                        $". До этого момента осталось всего {GenerateRemainingDaysMessageInRussian(difference)}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send message");
                }
            }
        }

        private static string GenerateRemainingDaysMessageInRussian(TimeSpan difference)
        {
            var days = "дней";
            var hours = "часов";
            var minutes = "минут";
            
            var daysLastOrLastTwoNumbers = difference.Days % 100;
            var hoursLastOrLastTwoNumbers = difference.Hours % 100;
            var minutesLastOrLastTwoNumbers = difference.Minutes % 100;

            if (daysLastOrLastTwoNumbers == 1)
            {
                days = "день";
            } 
            
            if (daysLastOrLastTwoNumbers > 1 && daysLastOrLastTwoNumbers < 5)
            {
                days = "дня";
            }

            if (hoursLastOrLastTwoNumbers == 1)
            {
                hours = "час";
            }

            if (hoursLastOrLastTwoNumbers > 1 && hoursLastOrLastTwoNumbers < 5)
            {
                hours = "часа";
            }

            if (minutesLastOrLastTwoNumbers == 1)
            {
                minutes = "минута";
            }

            if (minutesLastOrLastTwoNumbers > 1 && minutesLastOrLastTwoNumbers < 5)
            {
                minutes = "минуты";
            }

            return $"{difference.Days} {days}, {difference.Hours} {hours}, {difference.Minutes} {minutes}";
        }
    }
}
