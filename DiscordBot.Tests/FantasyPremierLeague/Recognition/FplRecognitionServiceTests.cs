using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recognition;
using DiscordBot.Tests;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Recognition;

[TestFixture]
public sealed class FplRecognitionServiceTests
{
    [Test]
    public void EvaluateAndPersist_RetryWithNewRuleVersion_ReturnsOriginalHistoricalResult()
    {
        // Arrange
        var snapshot = new FplGameweekSnapshot(
            "2026/27",
            5,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            [
                new FplManagerGameweekStatistics(
                    10,
                    "Alpha",
                    "Manager Alpha",
                    50,
                    100,
                    1,
                    1,
                    0,
                    7,
                    [
                        new FplLineupPick(1, "Captain", 1, 2, true, false, 5),
                        new FplLineupPick(2, "Vice", 2, 1, false, true, 5),
                        new FplLineupPick(3, "Bench", 12, 0, false, false, 7)
                    ])
            ]);
        var statisticsStore = new InMemoryFplStatisticsStore(snapshot);
        var recognitionStore = new InMemoryFplRecognitionStore();
        var firstOptions = new FantasyPremierLeagueOptions
        {
            ClassicLeagueId = 123,
            LargeBenchPointsThreshold = 8,
            RecognitionRuleVersion = "v1"
        };
        var firstService = CreateService(
            firstOptions,
            statisticsStore,
            recognitionStore);

        // Act
        var firstResult = firstService.EvaluateAndPersist(snapshot);
        var secondOptions = new FantasyPremierLeagueOptions
        {
            ClassicLeagueId = 123,
            LargeBenchPointsThreshold = 7,
            RecognitionRuleVersion = "v2"
        };
        var retryResult = CreateService(
            secondOptions,
            statisticsStore,
            recognitionStore).EvaluateAndPersist(snapshot);

        // Assert
        firstResult.Achievements.Should().NotContain(award =>
            award.AchievementKey == "bench-warmer");
        retryResult.Should().BeEquivalentTo(firstResult);
        retryResult.Ratings.Should().OnlyContain(rating => rating.RuleVersion == "v1");
    }

    private static FplRecognitionService CreateService(
        FantasyPremierLeagueOptions options,
        IFplStatisticsStore statisticsStore,
        IFplRecognitionStore recognitionStore)
    {
        return new FplRecognitionService(
            options,
            statisticsStore,
            recognitionStore,
            new FplAchievementCalculationService(options),
            new FplRatingCalculationService(options),
            new FixedTimeProvider(),
            new RecordingLogger<FplRecognitionService>());
    }

    private sealed class InMemoryFplStatisticsStore(FplGameweekSnapshot snapshot)
        : IFplStatisticsStore
    {
        public bool IsSnapshotStored(string season, int eventId)
        {
            return snapshot.Season == season && snapshot.EventId == eventId;
        }

        public void SaveSnapshot(FplGameweekSnapshot value)
        {
            throw new NotSupportedException();
        }

        public FplGameweekSnapshot? GetSnapshot(string season, int eventId)
        {
            return IsSnapshotStored(season, eventId) ? snapshot : null;
        }

        public IReadOnlyList<FplGameweekSnapshot> GetSnapshots(
            string season,
            int? eventId = null,
            int? managerEntryId = null)
        {
            return IsSnapshotStored(season, eventId ?? snapshot.EventId)
                ? [snapshot]
                : [];
        }
    }

    private sealed class InMemoryFplRecognitionStore : IFplRecognitionStore
    {
        private readonly Dictionary<(int LeagueId, string Season, int EventId), FplRecognitionResult>
            _results = [];
        private readonly List<FplAchievementAward> _awards = [];

        public FplRecognitionResult? GetCompletedResult(
            int leagueId,
            string season,
            int eventId)
        {
            return _results.GetValueOrDefault((leagueId, season, eventId));
        }

        public IReadOnlyList<FplAchievementAward> GetAchievementAwards(
            int leagueId,
            string season,
            int? eventId = null)
        {
            return _awards
                .Where(award => award.LeagueId == leagueId && award.Season == season)
                .Where(award => eventId is null || award.EventId == eventId)
                .ToArray();
        }

        public IReadOnlyList<FplManagerRating> GetManagerRatings(
            int leagueId,
            string season,
            int? eventId = null)
        {
            return _results
                .Where(pair =>
                    pair.Key.LeagueId == leagueId &&
                    pair.Key.Season == season &&
                    (eventId is null || pair.Key.EventId == eventId))
                .SelectMany(pair => pair.Value.Ratings)
                .ToArray();
        }

        public void Save(FplRecognitionRun run, FplRecognitionResult result)
        {
            var key = (run.LeagueId, run.Season, run.EventId);
            if (_results.ContainsKey(key))
            {
                return;
            }

            _results.Add(key, result);
            _awards.AddRange(result.Achievements);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(
                2026,
                8,
                21,
                19,
                0,
                0,
                TimeSpan.Zero);
        }
    }
}
