using AwesomeAssertions;
using DiscordBot.BenchWarming;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.Responses;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DiscordBot.Tests.BenchWarming;

[TestFixture]
public sealed class BenchWarmingLeagueCalculationServiceTests
{
    private string _testDirectory = null!;
    private string _databasePath = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "MandarinBotNet.Tests",
            Guid.NewGuid().ToString("N"));
        _databasePath = Path.Combine(_testDirectory, "bench-warming-league.db");
    }

    [TearDown]
    public void TearDown()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Test]
    public async Task CalculateLatestFinishedRoundAsync_BenchedPlayers_PersistsOnlyBenchPoints()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient();
        var store = new SqliteBenchWarmingLeagueStore(_databasePath);
        var service = CreateService(client, store);

        // Act
        var round = await service.CalculateLatestFinishedRoundAsync(
            CancellationToken.None);

        // Assert
        round.Should().NotBeNull();
        round!.Season.Should().Be("2026/27");
        round.EventId.Should().Be(5);
        round.WasAlreadyCalculated.Should().BeFalse();
        round.BenchPoints.Should().BeEquivalentTo(
            [
                new BenchWarmingPlayerPoints(100, "Team A", 1, "Haaland", 15),
                new BenchWarmingPlayerPoints(200, "Team B", 3, "Kepa", 0)
            ]);
        round.RoundStandings.Should().BeEquivalentTo(
            [
                new BenchWarmingEntryStanding(100, "Team A", 15),
                new BenchWarmingEntryStanding(200, "Team B", 0)
            ],
            options => options.WithStrictOrdering());
        round.SeasonStandings.Should().BeEquivalentTo(round.RoundStandings);
        store.IsRoundCalculated("2026/27", 5).Should().BeTrue();
    }

    [Test]
    public async Task CalculateLatestFinishedRoundAsync_BenchBoostManager_PersistsZeroEntryRound()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = CreateBootstrap(
                elements:
                [
                    CreateElement(1, "Haaland"),
                    CreateElement(2, "Salah"),
                    CreateElement(3, "Kepa"),
                    CreateElement(4, "Palmer")
                ],
                events:
                [
                    CreateEvent(id: 5, isFinished: true),
                    CreateEvent(id: 6, isFinished: false)
                ]),
            Standings = new ClassicStandingsResponse
            {
                Standings = new ClassicStandings
                {
                    Results =
                    [
                        new ClassicStanding { Entry = 100, EntryName = "Team A" },
                        new ClassicStanding { Entry = 200, EntryName = "Team B" },
                        new ClassicStanding { Entry = 300, EntryName = "Team C" }
                    ]
                }
            },
            Live = CreateLive((1, 15), (2, 7), (3, 0), (4, 9)),
            PicksByEntry = new Dictionary<int, EntryEventPicksResponse>
            {
                [100] = CreatePicks((1, 0), (2, 1)),
                [200] = CreatePicks((3, 0)),
                [300] = CreatePicks("bboost", (4, 1))
            }
        };
        var store = new SqliteBenchWarmingLeagueStore(_databasePath);
        var service = CreateService(client, store);

        // Act
        var round = await service.CalculateLatestFinishedRoundAsync(
            CancellationToken.None);

        // Assert
        round!.BenchPoints.Should().NotContain(benchPoint => benchPoint.EntryId == 300);
        round.RoundStandings.Should().Contain(
            new BenchWarmingEntryStanding(300, "Team C", 0));

        var entryRounds = store.GetSeasonRoundStandings("2026/27");
        entryRounds.Should().Contain(
            new BenchWarmingEntryRoundStanding(5, 300, "Team C", 0, "bboost"));
    }

    [Test]
    public async Task CalculateLatestFinishedRoundAsync_AlreadyCalculated_SkipsWithoutRefetching()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient();
        var store = new SqliteBenchWarmingLeagueStore(_databasePath);
        var service = CreateService(client, store);
        await service.CalculateLatestFinishedRoundAsync(CancellationToken.None);

        // Act
        var round = await service.CalculateLatestFinishedRoundAsync(
            CancellationToken.None);

        // Assert
        round.Should().NotBeNull();
        round!.WasAlreadyCalculated.Should().BeTrue();
        client.EntryPicksCallCount.Should().Be(2);
    }

    [Test]
    public async Task CalculateLatestFinishedRoundAsync_NoFinishedEvent_ReturnsNull()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = CreateBootstrap(
                elements: [],
                events:
                [
                    CreateEvent(id: 5, isFinished: false)
                ])
        };
        var service = CreateService(
            client,
            new SqliteBenchWarmingLeagueStore(_databasePath));

        // Act
        var round = await service.CalculateLatestFinishedRoundAsync(
            CancellationToken.None);

        // Assert
        round.Should().BeNull();
    }

    [Test]
    public async Task CalculateLatestFinishedRoundAsync_CurrentPayload_DerivesSeasonFromDeadlines()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = CreateBootstrap(
                elements: [],
                events: [CreateEvent(id: 5, isFinished: true)])
        };
        var store = new SqliteBenchWarmingLeagueStore(_databasePath);
        var service = CreateService(client, store);

        // Act
        var round = await service.CalculateLatestFinishedRoundAsync(
            CancellationToken.None);

        // Assert
        round!.Season.Should().Be("2026/27");
        store.GetLatestSeason().Should().Be("2026/27");
    }

    [Test]
    public async Task CalculateLatestFinishedRoundAsync_MultipleFinishedEvents_UsesMostRecent()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = CreateBootstrap(
                elements: [CreateElement(1, "Haaland")],
                events:
                [
                    CreateEvent(id: 4, isFinished: true),
                    CreateEvent(id: 5, isFinished: true),
                    CreateEvent(id: 6, isFinished: false)
                ])
        };
        var service = CreateService(
            client,
            new SqliteBenchWarmingLeagueStore(_databasePath));

        // Act
        var round = await service.CalculateLatestFinishedRoundAsync(
            CancellationToken.None);

        // Assert
        round!.EventId.Should().Be(5);
        client.RequestedEventId.Should().Be(5);
    }

    private static BenchWarmingLeagueCalculationService CreateService(
        TestFantasyPremierLeagueClient client,
        SqliteBenchWarmingLeagueStore store)
    {
        return new BenchWarmingLeagueCalculationService(
            client,
            new FantasyPremierLeagueOptions { ClassicLeagueId = 123 },
            store,
            new FixedTimeProvider(DateTimeOffset.UtcNow),
            new RecordingLogger<BenchWarmingLeagueCalculationService>());
    }

    private static BootstrapStaticResponse CreateBootstrap(
        List<PremierLeagueElement> elements,
        List<PremierLeagueEvent> events)
    {
        return new BootstrapStaticResponse
        {
            Elements = elements,
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
            DeadlineTimeEpoch = 1_787_333_400
        };
    }

    private static PremierLeagueElement CreateElement(int id, string webName)
    {
        return new PremierLeagueElement { Id = id, WebName = webName };
    }

    private static ClassicStandingsResponse CreateStandings()
    {
        return new ClassicStandingsResponse
        {
            Standings = new ClassicStandings
            {
                Results =
                [
                    new ClassicStanding { Entry = 100, EntryName = "Team A" },
                    new ClassicStanding { Entry = 200, EntryName = "Team B" }
                ]
            }
        };
    }

    private static EntryEventPicksResponse CreatePicks(params (int Element, int Multiplier)[] picks)
        => CreatePicks(null, picks);

    private static EntryEventPicksResponse CreatePicks(
        string? activeChip,
        params (int Element, int Multiplier)[] picks)
    {
        return new EntryEventPicksResponse
        {
            ActiveChip = activeChip,
            Picks = picks
                .Select(pick => new EntryEventPick
                {
                    Element = pick.Element,
                    Multiplier = pick.Multiplier,
                    Position = pick.Multiplier == 0 ? 12 : 1
                })
                .ToList()
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
        public BootstrapStaticResponse Bootstrap { get; init; } = new()
        {
            Elements =
            [
                CreateElement(1, "Haaland"),
                CreateElement(2, "Salah"),
                CreateElement(3, "Kepa")
            ],
            Events =
            [
                CreateEvent(id: 5, isFinished: true),
                CreateEvent(id: 6, isFinished: false)
            ]
        };

        public ClassicStandingsResponse Standings { get; init; } = CreateStandings();

        public EventLiveResponse Live { get; init; } = CreateLive(
            (1, 15),
            (2, 7),
            (3, 0));

        public Dictionary<int, EntryEventPicksResponse> PicksByEntry { get; init; } = new()
        {
            [100] = CreatePicks((1, 0), (2, 1)),
            [200] = CreatePicks((3, 0))
        };

        public int EntryPicksCallCount { get; private set; }

        public int RequestedEventId { get; private set; }

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
            RequestedEventId = eventId;
            return Task.FromResult(PicksByEntry[entryId]);
        }

        public Task<EventLiveResponse> GetEventLiveAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            RequestedEventId = eventId;
            return Task.FromResult(Live);
        }

        public Task<IReadOnlyList<PremierLeagueFixture>> GetFixturesAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
