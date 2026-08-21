using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recap;
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
        var collectionService = new FplStatisticsCollectionService(
            client,
            new FantasyPremierLeagueOptions { ClassicLeagueId = 123 },
            store,
            new FixedTimeProvider(),
            new RecordingLogger<FplStatisticsCollectionService>());
        var service = new FplGameweekRecapService(
            client,
            store,
            collectionService,
            new FplGameweekRecapCalculationService(),
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
        var collectionService = new FplStatisticsCollectionService(
            client,
            new FantasyPremierLeagueOptions { ClassicLeagueId = 123 },
            store,
            new FixedTimeProvider(),
            new RecordingLogger<FplStatisticsCollectionService>());
        var service = new FplGameweekRecapService(
            client,
            store,
            collectionService,
            new FplGameweekRecapCalculationService(),
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
                SeasonName = "2026/27",
                Events =
                [
                    new PremierLeagueEvent
                    {
                        Id = 5,
                        IsFinished = true,
                        DeadlineTimeEpoch = 1_700_000_000
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
                        Multiplier = 0
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
