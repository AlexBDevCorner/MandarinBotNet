using System.Globalization;
using Discord.WebSocket;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using Quartz;

namespace DiscordBot.Jobs;

[DisallowConcurrentExecution]
public sealed class PremierLeagueH2hStandingsInformationJob(
    DiscordSocketClient discordClient,
    IDiscordConnectionReadiness discordReadiness,
    IFantasyPremierLeagueClient premierLeagueClient,
    NotificationDeliveryCoordinator deliveryCoordinator,
    PremierLeagueMessageCompositionService messageComposer) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await discordReadiness.WaitUntilReadyAsync(context.CancellationToken);

        var standings = await premierLeagueClient.GetHeadToHeadStandingsAsync(
            1671824,
            context.CancellationToken);
        var results = standings.HeadToHeadStandings.Results;
        if (results.Count == 0)
        {
            return;
        }

        var message = messageComposer.ComposeHeadToHeadStandings(results);

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
                    results[0].MatchesPlayed.ToString(CultureInfo.InvariantCulture),
                    NotificationTypes.HeadToHeadStandings);

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
