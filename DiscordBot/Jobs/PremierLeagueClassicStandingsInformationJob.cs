using Discord.WebSocket;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using Quartz;

namespace DiscordBot.Jobs;

[DisallowConcurrentExecution]
public sealed class PremierLeagueClassicStandingsInformationJob(
    DiscordSocketClient discordClient,
    IDiscordConnectionReadiness discordReadiness,
    IFantasyPremierLeagueClient premierLeagueClient,
    NotificationDeliveryCoordinator deliveryCoordinator,
    StandingsPublicationEligibilityService publicationEligibility,
    StandingsChangeService standingsChangeService,
    WinnerSelectionService winnerSelection,
    PremierLeagueMessageCompositionService messageComposer,
    TimeProvider timeProvider) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await discordReadiness.WaitUntilReadyAsync(context.CancellationToken);

        var standings = await premierLeagueClient.GetClassicStandingsAsync(
            1671531,
            context.CancellationToken);
        if (!publicationEligibility.IsUpdatedSinceYesterday(
            standings.LastUpdatedData,
            timeProvider.GetUtcNow()))
        {
            return;
        }

        var results = standings.Standings.Results;
        var message = messageComposer.ComposeClassicStandings(
            results,
            winnerSelection.SelectEventWinner(results),
            standingsChangeService.GetChanges(results),
            Random.Shared.Next(
                PremierLeagueMessageCompositionService.ClassicCongratulationsVariantCount));

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
                    standings.LastUpdatedData.ToUniversalTime().ToString("O"),
                    NotificationTypes.ClassicStandings);

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
