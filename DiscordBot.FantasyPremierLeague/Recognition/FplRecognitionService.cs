using DiscordBot.FantasyPremierLeague.Historical;
using Microsoft.Extensions.Logging;

namespace DiscordBot.FantasyPremierLeague.Recognition;

public sealed class FplRecognitionService(
    FantasyPremierLeagueOptions options,
    IFplStatisticsStore statisticsStore,
    IFplRecognitionStore recognitionStore,
    FplAchievementCalculationService achievementCalculationService,
    TimeProvider timeProvider,
    ILogger<FplRecognitionService> logger)
{
    public FplRecognitionResult EvaluateAndPersist(FplGameweekSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (options.ClassicLeagueId <= 0)
        {
            throw new InvalidOperationException(
                "The configured classic FPL league ID must be positive before recognition " +
                "can be calculated.");
        }

        var persistedResult = recognitionStore.GetCompletedResult(
            options.ClassicLeagueId,
            snapshot.Season,
            snapshot.EventId);
        if (persistedResult is not null)
        {
            logger.LogInformation(
                "Recognition for league {LeagueId}, season {Season}, event {EventId} " +
                "was already persisted; returning the historical result.",
                options.ClassicLeagueId,
                snapshot.Season,
                snapshot.EventId);
            return persistedResult;
        }

        var seasonHistory = statisticsStore.GetSnapshots(snapshot.Season);
        var existingAwards = recognitionStore.GetAchievementAwards(
            options.ClassicLeagueId,
            snapshot.Season);
        var calculatedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        var awards = achievementCalculationService.Calculate(
            options.ClassicLeagueId,
            snapshot,
            seasonHistory,
            existingAwards,
            calculatedAtUtc);

        recognitionStore.Save(
            new FplRecognitionRun(
                options.ClassicLeagueId,
                snapshot.Season,
                snapshot.EventId,
                options.RecognitionRuleVersion,
                calculatedAtUtc),
            new FplRecognitionResult(awards));

        var persistedRecognition = recognitionStore.GetCompletedResult(
            options.ClassicLeagueId,
            snapshot.Season,
            snapshot.EventId);
        if (persistedRecognition is null)
        {
            throw new InvalidOperationException(
                "The recognition store did not return the result that was just persisted.");
        }

        logger.LogInformation(
            "Persisted FPL recognition for league {LeagueId}, season {Season}, event " +
            "{EventId}: {AchievementCount} achievements.",
            options.ClassicLeagueId,
            snapshot.Season,
            snapshot.EventId,
            persistedRecognition.Achievements.Count);
        return persistedRecognition;
    }
}
