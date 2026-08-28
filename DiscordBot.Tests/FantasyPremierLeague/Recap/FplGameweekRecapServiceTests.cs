using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recap;
using DiscordBot.FantasyPremierLeague.Recognition;
using DiscordBot.Responses;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Recap;

[TestFixture]
public sealed class FplGameweekRecapServiceTests
{
    [Test]
    public async Task GetLatestAsync_MissingSnapshot_CollectsAndReturnsRecap()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient();
        var store = new InMemoryFplStatisticsStore();
        var options = new FantasyPremierLeagueOptions { ClassicLeagueId = 123 };
        var collectionService = new FplStatisticsCollectionService(
            client,
            options,
            store,
            new FixedTimeProvider(),
            new RecordingLogger<FplStatisticsCollectionService>());
        var recognitionService = CreateRecognitionService(options, store);
        var service = new FplGameweekRecapService(
            client,
            store,
            collectionService,
            new FplGameweekRecapCalculationService(),
            recognitionService,
            new RecordingLogger<FplGameweekRecapService>());

        // Act
        var recap = await service.GetLatestAsync(CancellationToken.None);

        // Assert
        recap.Should().NotBeNull();
        recap!.Season.Should().Be("2026/27");
        recap.EventId.Should().Be(5);
        recap.HighestScorers.Should().ContainSingle().Which.EntryName.Should().Be("Team A");
        store.GetSnapshot("2026/27", 5).Should().NotBeNull();
    }

    [Test]
    public async Task GetLatestAsync_IncompleteStoredSnapshot_LogsAndSkipsPublication()
    {
        // Arrange
        var logger = new RecordingLogger<FplGameweekRecapService>();
        var store = new InMemoryFplStatisticsStore();
        store.SaveSnapshot(new FplGameweekSnapshot(
            "2026/27",
            5,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            [
                new FplManagerGameweekStatistics(
                    100,
                    "Incomplete",
                    "Alice",
                    50,
                    100,
                    1,
                    1,
                    0,
                    0,
                    [])
            ]));
        var client = new TestFantasyPremierLeagueClient();
        var options = new FantasyPremierLeagueOptions { ClassicLeagueId = 123 };
        var collectionService = new FplStatisticsCollectionService(
            client,
            options,
            store,
            new FixedTimeProvider(),
            new RecordingLogger<FplStatisticsCollectionService>());
        var recognitionService = CreateRecognitionService(options, store);
        var service = new FplGameweekRecapService(
            client,
            store,
            collectionService,
            new FplGameweekRecapCalculationService(),
            recognitionService,
            logger);

        // Act
        var recap = await service.GetLatestAsync(CancellationToken.None);

        // Assert
        recap.Should().BeNull();
        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("source data was unavailable or incomplete"));
    }

    private sealed class TestFantasyPremierLeagueClient : IFantasyPremierLeagueClient
    {
        public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new BootstrapStaticResponse
            {
                Events =
                [
                    new PremierLeagueEvent
                    {
                        Id = 5,
                        IsFinished = true,
                        DeadlineTimeEpoch = 1_787_333_400
                    }
                ],
                Elements =
                [
                    new PremierLeagueElement { Id = 1, WebName = "Player One" },
                    new PremierLeagueElement { Id = 2, WebName = "Player Two" }
                ]
            });
        }

        public Task<ClassicStandingsResponse> GetClassicStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ClassicStandingsResponse
            {
                LastUpdatedData = DateTimeOffset.UtcNow,
                Standings = new ClassicStandings
                {
                    Results =
                    [
                        new ClassicStanding
                        {
                            Entry = 100,
                            EntryName = "Team A",
                            PlayerName = "Alice",
                            EventTotal = 50,
                            Total = 100,
                            Rank = 1,
                            LastRank = 2
                        }
                    ]
                }
            });
        }

        public Task<HeadToHeadStandingsResponse> GetHeadToHeadStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<EntryEventPicksResponse> GetEntryEventPicksAsync(
            int entryId,
            int eventId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new EntryEventPicksResponse
            {
                Picks =
                [
                    new EntryEventPick
                    {
                        Element = 1,
                        Position = 1,
                        Multiplier = 2,
                        IsCaptain = true
                    },
                    new EntryEventPick
                    {
                        Element = 2,
                        Position = 12,
                        Multiplier = 0,
                        IsViceCaptain = true
                    }
                ]
            });
        }

        public Task<EventLiveResponse> GetEventLiveAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new EventLiveResponse
            {
                Elements =
                [
                    new EventLiveElement
                    {
                        Id = 1,
                        Stats = new EventLiveElementStats { TotalPoints = 10 }
                    },
                    new EventLiveElement
                    {
                        Id = 2,
                        Stats = new EventLiveElementStats { TotalPoints = 0 }
                    }
                ]
            });
        }

        public Task<IReadOnlyList<PremierLeagueFixture>> GetFixturesAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class InMemoryFplStatisticsStore : IFplStatisticsStore
    {
        private readonly List<FplGameweekSnapshot> _snapshots = [];

        public bool IsSnapshotStored(string season, int eventId)
        {
            return _snapshots.Any(snapshot =>
                snapshot.Season == season && snapshot.EventId == eventId);
        }

        public void SaveSnapshot(FplGameweekSnapshot snapshot)
        {
            _snapshots.RemoveAll(item =>
                item.Season == snapshot.Season && item.EventId == snapshot.EventId);
            _snapshots.Add(snapshot);
        }

        public FplGameweekSnapshot? GetSnapshot(string season, int eventId)
        {
            return _snapshots.FirstOrDefault(snapshot =>
                snapshot.Season == season && snapshot.EventId == eventId);
        }

        public IReadOnlyList<FplGameweekSnapshot> GetSnapshots(
            string season,
            int? eventId = null,
            int? managerEntryId = null)
        {
            return _snapshots
                .Where(snapshot => snapshot.Season == season)
                .Where(snapshot => eventId is null || snapshot.EventId == eventId)
                .ToArray();
        }
    }

    private static FplRecognitionService CreateRecognitionService(
        FantasyPremierLeagueOptions options,
        IFplStatisticsStore statisticsStore)
    {
        return new FplRecognitionService(
            options,
            statisticsStore,
            new InMemoryFplRecognitionStore(),
            new FplAchievementCalculationService(options),
            new FplRatingCalculationService(options),
            new FixedTimeProvider(),
            new RecordingLogger<FplRecognitionService>());
    }

    private sealed class InMemoryFplRecognitionStore : IFplRecognitionStore
    {
        private readonly List<FplAchievementAward> _awards = [];
        private readonly List<FplManagerRating> _ratings = [];
        private readonly Dictionary<(int LeagueId, string Season, int EventId), FplRecognitionResult>
            _completedResults = [];

        public FplRecognitionResult? GetCompletedResult(
            int leagueId,
            string season,
            int eventId)
        {
            return _completedResults.GetValueOrDefault((leagueId, season, eventId));
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
            return _ratings
                .Where(rating => rating.LeagueId == leagueId && rating.Season == season)
                .Where(rating => eventId is null || rating.EventId == eventId)
                .ToArray();
        }

        public void Save(FplRecognitionRun run, FplRecognitionResult result)
        {
            if (!_completedResults.ContainsKey((run.LeagueId, run.Season, run.EventId)))
            {
                _awards.AddRange(result.Achievements);
                _ratings.AddRange(result.Ratings);
                _completedResults.Add(
                    (run.LeagueId, run.Season, run.EventId),
                    result);
            }
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
