using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.Responses;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Historical;

[TestFixture]
public sealed class FplStatisticsCollectionServiceTests
{
    [Test]
    public async Task CollectLatestMissingGameweekAsync_CompletedGameweek_PersistsHistoricalSnapshot()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient();
        var store = new InMemoryFplStatisticsStore();
        var service = CreateService(client, store);

        // Act
        var snapshot = await service.CollectLatestMissingGameweekAsync(
            CancellationToken.None);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot!.Season.Should().Be("2026/27");
        snapshot.EventId.Should().Be(5);
        snapshot.DeadlineUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
        snapshot.Managers.Should().HaveCount(2);
        snapshot.Managers[0].Should().BeEquivalentTo(
            new FplManagerGameweekStatistics(
                100,
                "Team A",
                "Alice",
                12,
                100,
                2,
                4,
                2,
                5,
                [
                    new FplLineupPick(1, "Haaland", 1, 0, false, false, 5),
                    new FplLineupPick(2, "Salah", 2, 2, true, false, 7)
                ]));
        store.GetSnapshot("2026/27", 5).Should().BeEquivalentTo(snapshot);
        client.RequestedEventIds.Should().OnlyContain(eventId => eventId == 5);
    }

    [Test]
    public async Task CollectLatestMissingGameweekAsync_AlreadyStoredGameweek_SkipsWithoutRefetching()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient();
        var store = new InMemoryFplStatisticsStore();
        var service = CreateService(client, store);
        await service.CollectLatestMissingGameweekAsync(CancellationToken.None);
        var entryPicksCallCount = client.EntryPicksCallCount;

        // Act
        var snapshot = await service.CollectLatestMissingGameweekAsync(
            CancellationToken.None);

        // Assert
        snapshot.Should().BeNull();
        client.EntryPicksCallCount.Should().Be(entryPicksCallCount);
        store.Snapshots.Should().ContainSingle();
    }

    [Test]
    public async Task CollectLatestMissingGameweekAsync_MissingSeasonName_RefusesToPersistSeasonlessData()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = CreateBootstrap(
                season: null,
                events: [CreateEvent(5, isFinished: true)])
        };
        var store = new InMemoryFplStatisticsStore();
        var service = CreateService(client, store);

        // Act
        Func<Task> act = () => service.CollectLatestMissingGameweekAsync(
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
        store.Snapshots.Should().BeEmpty();
    }

    [Test]
    public async Task CollectLatestMissingGameweekAsync_MissingLivePlayerPoints_DoesNotPersistPartialData()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Live = CreateLive((1, 5))
        };
        var store = new InMemoryFplStatisticsStore();
        var service = CreateService(client, store);

        // Act
        Func<Task> act = () => service.CollectLatestMissingGameweekAsync(
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
        store.Snapshots.Should().BeEmpty();
    }

    [Test]
    public async Task CollectLatestMissingGameweekAsync_StorageFailure_LogsAndRethrows()
    {
        // Arrange
        var logger = new RecordingLogger<FplStatisticsCollectionService>();
        var store = new FailingFplStatisticsStore();
        var service = CreateService(
            new TestFantasyPremierLeagueClient(),
            store,
            logger);

        // Act
        Func<Task> act = () => service.CollectLatestMissingGameweekAsync(
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        var logEntry = logger.Entries.Should().ContainSingle().Which;
        logEntry.Level.Should().Be(LogLevel.Error);
        logEntry.Message.Should().Contain("Failed to persist historical FPL statistics");
        logEntry.Properties["Season"].Should().Be("2026/27");
        logEntry.Properties["EventId"].Should().Be(5);
    }

    private static FplStatisticsCollectionService CreateService(
        IFantasyPremierLeagueClient client,
        IFplStatisticsStore store,
        ILogger<FplStatisticsCollectionService>? logger = null)
    {
        return new FplStatisticsCollectionService(
            client,
            new FantasyPremierLeagueOptions { ClassicLeagueId = 123 },
            store,
            new FixedTimeProvider(new DateTimeOffset(
                2026,
                8,
                21,
                19,
                0,
                0,
                TimeSpan.Zero)),
            logger ?? new RecordingLogger<FplStatisticsCollectionService>());
    }

    private static BootstrapStaticResponse CreateBootstrap(
        string? season,
        List<PremierLeagueEvent> events)
    {
        return new BootstrapStaticResponse
        {
            SeasonName = season,
            Elements =
            [
                new PremierLeagueElement { Id = 1, WebName = "Haaland" },
                new PremierLeagueElement { Id = 2, WebName = "Salah" },
                new PremierLeagueElement { Id = 3, WebName = "Kepa" }
            ],
            Events = events
        };
    }

    private static PremierLeagueEvent CreateEvent(int id, bool isFinished)
    {
        return new PremierLeagueEvent
        {
            Id = id,
            IsFinished = isFinished,
            IsNext = !isFinished,
            DeadlineTimeEpoch = 1_700_000_000
        };
    }

    private static EventLiveResponse CreateLive(params (int Id, int Points)[] elements)
    {
        return new EventLiveResponse
        {
            Elements = elements
                .Select(element => new EventLiveElement
                {
                    Id = element.Id,
                    Stats = new EventLiveElementStats { TotalPoints = element.Points }
                })
                .ToList()
        };
    }

    private sealed class TestFantasyPremierLeagueClient : IFantasyPremierLeagueClient
    {
        public BootstrapStaticResponse Bootstrap { get; init; } = CreateBootstrap(
            "2026/27",
            [
                CreateEvent(4, isFinished: true),
                CreateEvent(5, isFinished: true),
                CreateEvent(6, isFinished: false)
            ]);

        public ClassicStandingsResponse Standings { get; init; } = new()
        {
            LastUpdatedData = new DateTimeOffset(
                2026,
                8,
                21,
                18,
                30,
                0,
                TimeSpan.Zero),
            Standings = new ClassicStandings
            {
                Results =
                [
                    new ClassicStanding
                    {
                        Entry = 100,
                        EntryName = "Team A",
                        PlayerName = "Alice",
                        EventTotal = 12,
                        Total = 100,
                        Rank = 2,
                        LastRank = 4
                    },
                    new ClassicStanding
                    {
                        Entry = 200,
                        EntryName = "Team B",
                        PlayerName = "Bob",
                        EventTotal = 8,
                        Total = 90,
                        Rank = 5,
                        LastRank = 3
                    }
                ]
            }
        };

        public EventLiveResponse Live { get; init; } = CreateLive(
            (1, 5),
            (2, 7),
            (3, 0));

        public int EntryPicksCallCount { get; private set; }

        public List<int> RequestedEventIds { get; } = [];

        public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Bootstrap);
        }

        public Task<ClassicStandingsResponse> GetClassicStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Standings);
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
            EntryPicksCallCount++;
            RequestedEventIds.Add(eventId);
            return Task.FromResult(new EntryEventPicksResponse
            {
                Picks = entryId switch
                {
                    100 =>
                    [
                        new EntryEventPick
                        {
                            Element = 1,
                            Position = 1,
                            Multiplier = 0
                        },
                        new EntryEventPick
                        {
                            Element = 2,
                            Position = 2,
                            Multiplier = 2,
                            IsCaptain = true
                        }
                    ],
                    200 =>
                    [
                        new EntryEventPick
                        {
                            Element = 3,
                            Position = 12,
                            Multiplier = 0
                        }
                    ],
                    _ => throw new NotSupportedException()
                }
            });
        }

        public Task<EventLiveResponse> GetEventLiveAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            RequestedEventIds.Add(eventId);
            return Task.FromResult(Live);
        }
    }

    private sealed class InMemoryFplStatisticsStore : IFplStatisticsStore
    {
        public List<FplGameweekSnapshot> Snapshots { get; } = [];

        public bool IsSnapshotStored(string season, int eventId)
        {
            return Snapshots.Any(snapshot =>
                snapshot.Season == season && snapshot.EventId == eventId);
        }

        public void SaveSnapshot(FplGameweekSnapshot snapshot)
        {
            Snapshots.RemoveAll(item =>
                item.Season == snapshot.Season && item.EventId == snapshot.EventId);
            Snapshots.Add(snapshot);
        }

        public FplGameweekSnapshot? GetSnapshot(string season, int eventId)
        {
            return Snapshots.FirstOrDefault(snapshot =>
                snapshot.Season == season && snapshot.EventId == eventId);
        }

        public IReadOnlyList<FplGameweekSnapshot> GetSnapshots(
            string season,
            int? eventId = null,
            int? managerEntryId = null)
        {
            return Snapshots
                .Where(snapshot => snapshot.Season == season)
                .Where(snapshot => eventId is null || snapshot.EventId == eventId)
                .ToList();
        }
    }

    private sealed class FailingFplStatisticsStore : IFplStatisticsStore
    {
        public bool IsSnapshotStored(string season, int eventId)
        {
            return false;
        }

        public void SaveSnapshot(FplGameweekSnapshot snapshot)
        {
            throw new InvalidOperationException("Simulated storage failure.");
        }

        public FplGameweekSnapshot? GetSnapshot(string season, int eventId)
        {
            return null;
        }

        public IReadOnlyList<FplGameweekSnapshot> GetSnapshots(
            string season,
            int? eventId = null,
            int? managerEntryId = null)
        {
            return [];
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
