using Discord.WebSocket;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.Notifications;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Globalization;

namespace DiscordBot.Jobs
{
    [DisallowConcurrentExecution]
    public class PremierLeagueNotificationJob(
        IDiscordConnectionReadiness discordReadiness,
        IFantasyPremierLeagueClient premierLeagueClient,
        IDiscordNotificationPublisher notificationPublisher,
        JobSchedulesOptions scheduleOptions,
        NotificationOptions notificationOptions,
        ILogger<PremierLeagueNotificationJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            await discordReadiness.WaitUntilReadyAsync(context.CancellationToken);

            var bootstrap = await premierLeagueClient.GetBootstrapStaticAsync(
                context.CancellationToken);
            var configuredTimeZone = JobSchedules.GetTimeZone(scheduleOptions);
            var nextEvent = bootstrap.Events.FirstOrDefault(item => item.IsNext);
            if (nextEvent is null) return;

            var dateTimeUtc = DateTimeOffset
                .FromUnixTimeSeconds(nextEvent.DeadlineTimeEpoch)
                .UtcDateTime;
            var deadline = TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, configuredTimeZone);

            var utcNow = DateTime.UtcNow;

            var currentLocalTime = TimeZoneInfo.ConvertTimeFromUtc(
                utcNow,
                configuredTimeZone);

            var beforeTime = deadline.AddHours(-24).AddMinutes(-58);
            var afterTime = deadline.AddHours(-23).AddMinutes(-2);
            var hourBefore = deadline.AddMinutes(-70);

            Console.WriteLine($"Before - {beforeTime}");
            Console.WriteLine($"After - {afterTime}");
            Console.WriteLine($"Hour before - {hourBefore}");
            Console.WriteLine($"Deadline - {deadline}");
            Console.WriteLine($"Current configured time - {currentLocalTime}");

            var notificationType =
                currentLocalTime > beforeTime && currentLocalTime < afterTime
                    ? NotificationTypes.Deadline24Hours
                    : currentLocalTime > hourBefore && currentLocalTime < deadline
                        ? NotificationTypes.Deadline1Hour
                        : null;

            if (notificationType is not null)
            {
                var now = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    configuredTimeZone);
                var difference = deadline.Subtract(now);
                var russianCulture = new CultureInfo("ru-RU");
                var message =
                    "Привет мои любители АПЛ и обнимашек! :people_hugging: Следующий тур уже скоро -" +
                    $" {deadline.ToString("dd MMMM yyyy, HH:mm", russianCulture)}, это {deadline.ToString("dddd", russianCulture)}" +
                    $". До этого момента осталось всего {DateTimeUtility.GenerateRemainingDaysMessageInRussian(difference)}.";

                foreach (var target in notificationOptions.Targets)
                {
                    try
                    {
                        await notificationPublisher.PublishOnceAsync(
                            target,
                            nextEvent.Id.ToString(CultureInfo.InvariantCulture),
                            notificationType,
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
                            "Failed to publish deadline notification to Discord guild {GuildId}, channel {ChannelId}.",
                            target.GuildId,
                            target.ChannelId);
                    }
                }
            }
        }
    }
}
