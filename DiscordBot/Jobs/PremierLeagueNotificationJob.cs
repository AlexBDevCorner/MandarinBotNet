using Discord.WebSocket;
using DiscordBot.Notifications;
using Quartz;
using System.Globalization;
using System.Text.Json;
using TimeZoneConverter;

namespace DiscordBot.Jobs
{
    [DisallowConcurrentExecution]
    public class PremierLeagueNotificationJob(
        DiscordSocketClient discordClient,
        IHttpClientFactory httpClientFactory,
        NotificationDeliveryCoordinator deliveryCoordinator) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://fantasy.premierleague.com");
            var response = await client.GetStreamAsync("/api/bootstrap-static");
            using JsonDocument jsonDoc = await JsonDocument.ParseAsync(response);
            DateTime? deadline = null;
            int? eventId = null;

            var rigaTimeZone = TZConvert.GetTimeZoneInfo("Europe/Riga");

            if (jsonDoc.RootElement.TryGetProperty("events", out JsonElement dataArrayElement))
            {
                foreach (JsonElement itemElement in dataArrayElement.EnumerateArray())
                {
                    var isNext = itemElement.GetProperty("is_next").ToString();

                    if (isNext is null) continue;

                    if (isNext.ToString() == "True")
                    {
                        eventId = itemElement.GetProperty("id").GetInt32();
                        var epochTime = itemElement.GetProperty("deadline_time_epoch").GetInt64();
                        var dateTimeUtc = DateTimeOffset.FromUnixTimeSeconds(epochTime).UtcDateTime;

                        deadline = TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, rigaTimeZone);
                    }
                }
            }

            if (deadline is null || eventId is null) return;

            var utcNow = DateTime.UtcNow;

            var currentRigaTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, rigaTimeZone);

            var beforeTime = deadline.Value.AddHours(-24).AddMinutes(-58);
            var afterTime = deadline.Value.AddHours(-23).AddMinutes(-2);
            var hourBefore = deadline.Value.AddMinutes(-70);

            Console.WriteLine($"Before - {beforeTime}");
            Console.WriteLine($"After - {afterTime}");
            Console.WriteLine($"Hour before - {hourBefore}");
            Console.WriteLine($"Deadline - {deadline}");
            Console.WriteLine($"Current Riga Time - {currentRigaTime}");

            var notificationType =
                currentRigaTime > beforeTime && currentRigaTime < afterTime
                    ? NotificationTypes.Deadline24Hours
                    : currentRigaTime > hourBefore && currentRigaTime < deadline
                        ? NotificationTypes.Deadline1Hour
                        : null;

            if (notificationType is not null)
            {
                foreach (var guild in discordClient.Guilds)
                {
                    var generalChannel = guild.TextChannels
                        .FirstOrDefault(c => c.Name.Equals("general", StringComparison.OrdinalIgnoreCase)
                            || c.Name.Equals("announcement-bot", StringComparison.OrdinalIgnoreCase));

                    if (generalChannel is null) continue;

                    try
                    {
                        var castedDeadline = (DateTime)deadline;
                        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, rigaTimeZone);
                        var difference = castedDeadline.Subtract(now);

                        var russianCulture = new CultureInfo("ru-RU");

                        var checkpoint = new NotificationCheckpoint(
                            guild.Id,
                            generalChannel.Id,
                            eventId.Value.ToString(CultureInfo.InvariantCulture),
                            notificationType);

                        await deliveryCoordinator.SendOnceAsync(
                            checkpoint,
                            () => generalChannel.SendMessageAsync(
                                $"@everyone Привет мои любители АПЛ и обнимашек! :people_hugging: Следующий тур уже скоро -" +
                                $" {castedDeadline.ToString("dd MMMM yyyy, HH:mm", russianCulture)}, это {castedDeadline.ToString("dddd", russianCulture)}" +
                                $". До этого момента осталось всего {DateTimeUtility.GenerateRemainingDaysMessageInRussian(difference)}."),
                            context.CancellationToken);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Failed to send message");
                    }
                }
            }
        }
    }
}
