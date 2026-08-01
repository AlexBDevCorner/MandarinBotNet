using System.Globalization;
using Discord.WebSocket;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using Quartz;

namespace DiscordBot.Jobs;

[DisallowConcurrentExecution]
public sealed class PremierLeagueNotificationJob(
    DiscordSocketClient discordClient,
    IDiscordConnectionReadiness discordReadiness,
    IFantasyPremierLeagueClient premierLeagueClient,
    NotificationDeliveryCoordinator deliveryCoordinator,
    DeadlineSelectionService deadlineSelection,
    ReminderEligibilityService reminderEligibility,
    PremierLeagueMessageCompositionService messageComposer,
    TimeProvider timeProvider) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await discordReadiness.WaitUntilReadyAsync(context.CancellationToken);

        var bootstrap = await premierLeagueClient.GetBootstrapStaticAsync(
            context.CancellationToken);
        var deadline = deadlineSelection.SelectNext(bootstrap.Events);
        if (deadline is null)
        {
            return;
        }

        var utcNow = timeProvider.GetUtcNow();
        var notificationType = reminderEligibility.SelectNotificationType(
            utcNow,
            deadline.DeadlineUtc);
        if (notificationType is null)
        {
            return;
        }

        var message = messageComposer.ComposeDeadlineReminder(
            deadline.DeadlineUtc,
            utcNow);

        foreach (var guild in discordClient.Guilds)
        {
            var generalChannel = guild.TextChannels.FirstOrDefault(
                channel => channel.Name.Equals(
                        "general",
                        StringComparison.OrdinalIgnoreCase)
                    || channel.Name.Equals(
                        "announcement-bot",
                        StringComparison.OrdinalIgnoreCase));

            if (generalChannel is null)
            {
                continue;
            }

            try
            {
                var checkpoint = new NotificationCheckpoint(
                    guild.Id,
                    generalChannel.Id,
                    deadline.EventId.ToString(CultureInfo.InvariantCulture),
                    notificationType);

                await deliveryCoordinator.SendOnceAsync(
                    checkpoint,
                    () => generalChannel.SendMessageAsync(message),
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
