using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recognition;
using Microsoft.Extensions.Logging;

namespace DiscordBot.FantasyPremierLeague.Recap;

public sealed class FplGameweekRecapService(
    IFantasyPremierLeagueClient premierLeagueClient,
    IFplStatisticsStore statisticsStore,
    FplStatisticsCollectionService collectionService,
    FplGameweekRecapCalculationService calculationService,
    FplRecognitionService recognitionService,
    ILogger<FplGameweekRecapService> logger)
{
    public async Task<FplGameweekRecap?> GetLatestAsync(
        CancellationToken cancellationToken)
    {
        string? season = null;
        int? eventId = null;

        try
        {
            var bootstrap = await premierLeagueClient.GetBootstrapStaticAsync(
                cancellationToken);
            season = FplSeasonName.FromEvents(bootstrap.Events);

            var finishedEvent = bootstrap.Events
                .Where(item => item.IsFinished)
                .OrderByDescending(item => item.Id)
                .FirstOrDefault();
            if (finishedEvent is null)
            {
                logger.LogInformation(
                    "FPL gameweek recap skipped because no finished gameweek was found " +
                    "for season {Season}.",
                    season);
                return null;
            }

            eventId = finishedEvent.Id;
            var snapshot = statisticsStore.GetSnapshot(season, finishedEvent.Id);
            if (snapshot is null)
            {
                snapshot = await collectionService.CollectLatestMissingGameweekAsync(
                    cancellationToken);
                snapshot ??= statisticsStore.GetSnapshot(season, finishedEvent.Id);
            }

            if (snapshot is null)
            {
                logger.LogWarning(
                    "FPL gameweek recap skipped because no complete snapshot was " +
                    "available for season {Season} event {EventId}.",
                    season,
                    finishedEvent.Id);
                return null;
            }

            if (snapshot.Season != season || snapshot.EventId != finishedEvent.Id)
            {
                logger.LogWarning(
                    "FPL gameweek recap skipped because the stored snapshot key " +
                    "does not match season {Season} event {EventId}.",
                    season,
                    finishedEvent.Id);
                return null;
            }

            var seasonHistory = statisticsStore.GetSnapshots(season);
            var recap = calculationService.Calculate(snapshot, seasonHistory);
            var recognition = recognitionService.EvaluateAndPersist(snapshot);
            return recap with
            {
                Achievements = recognition.Achievements
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "FPL gameweek recap skipped because source data was unavailable or " +
                "incomplete for season {Season} event {EventId}.",
                season ?? "unknown",
                eventId ?? 0);
            return null;
        }
    }
}
