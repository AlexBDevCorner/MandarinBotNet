using Discord.WebSocket;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.Notifications;
using Quartz;
using System.Globalization;
using TimeZoneConverter;

namespace DiscordBot.Jobs
{
    [DisallowConcurrentExecution]
    public class PremierLeagueNotificationJob(
        DiscordSocketClient discordClient,
        IDiscordConnectionReadiness discordReadiness,
        IFantasyPremierLeagueClient premierLeagueClient,
        NotificationDeliveryCoordinator deliveryCoordinator) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            await discordReadiness.WaitUntilReadyAsync(context.CancellationToken);

            var bootstrap = await premierLeagueClient.GetBootstrapStaticAsync(
                context.CancellationToken);
            var rigaTimeZone = TZConvert.GetTimeZoneInfo("Europe/Riga");
            var nextEvent = bootstrap.Events.FirstOrDefault(item => item.IsNext);
            if (nextEvent is null) return;

            var dateTimeUtc = DateTimeOffset
                .FromUnixTimeSeconds(nextEvent.DeadlineTimeEpoch)
                .UtcDateTime;
            var deadline = TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, rigaTimeZone);

            var utcNow = DateTime.UtcNow;

            var currentRigaTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, rigaTimeZone);

            var beforeTime = deadline.AddHours(-24).AddMinutes(-58);
            var afterTime = deadline.AddHours(-23).AddMinutes(-2);
            var hourBefore = deadline.AddMinutes(-70);

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
                        var castedDeadline = deadline;
                        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, rigaTimeZone);
                        var difference = castedDeadline.Subtract(now);

                        var russianCulture = new CultureInfo("ru-RU");

                        var checkpoint = new NotificationCheckpoint(
                            guild.Id,
                            generalChannel.Id,
                            nextEvent.Id.ToString(CultureInfo.InvariantCulture),
                            notificationType);

                        await deliveryCoordinator.SendOnceAsync(
                            checkpoint,
                            () => generalChannel.SendMessageAsync(
                                $"@everyone Привет мои любители АПЛ и обнимашек! :people_hugging: Следующий тур уже скоро -" +
                                $" {castedDeadline.ToString("dd MMMM yyyy, HH:mm", russianCulture)}, это {castedDeadline.ToString("dddd", russianCulture)}" +
                                $". До этого момента осталось всего {DateTimeUtility.GenerateRemainingDaysMessageInRussian(difference)}."),
                            context.CancellationToken);
                    }
                    catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                    {
                        throw;
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
