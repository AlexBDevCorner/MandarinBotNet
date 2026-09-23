using System.Globalization;
using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.PriceChanges;
using DiscordBot.Responses;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.PriceChanges;

[TestFixture]
public sealed class FplLeaguePriceChangeServiceTests
{
    [Test]
    public async Task CreateReportAsync_LeagueSquads_MapsOwnersAndTeamImpacts()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Managers =
            [
                CreateManager(1, "Bobrov FC"),
                CreateManager(2, "Fraud United")
            ]
        };
        client.Picks[1] = CreatePicks(10, 20);
        client.Picks[2] = CreatePicks(20);
        var service = CreateService(client);
        var changes = new FplPlayerPriceChange[]
        {
            new(10, "Salah", 100, 101),
            new(20, "Haaland", 150, 149),
            new(30, "Saka", 100, 101)
        };

        // Act
        var report = await service.CreateReportAsync(
            changes,
            DateTimeOffset.Parse("2026-08-30T10:00:00Z", CultureInfo.InvariantCulture),
            eventId: 3,
            CancellationToken.None);

        // Assert
        report.LeagueDataAvailable.Should().BeTrue();
        report.LeagueManagerCount.Should().Be(2);
        report.AvailableSquadCount.Should().Be(2);
        report.PlayerChanges[0].OwnerEntryNames.Should().Equal("Bobrov FC");
        report.PlayerChanges[1].OwnerEntryNames.Should()
            .Equal("Bobrov FC", "Fraud United");
        report.PlayerChanges[2].OwnerEntryNames.Should().BeEmpty();
        report.TeamImpacts.Should().Equal(
            new FplTeamPriceImpact(2, "Fraud United", -1));
    }

    [Test]
    public async Task CreateReportAsync_OneSquadFails_ReturnsPartialCoverage()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Managers =
            [
                CreateManager(1, "Bobrov FC"),
                CreateManager(2, "Fraud United")
            ]
        };
        client.Picks[1] = CreatePicks(10);
        client.PickFailures.Add(2);
        var service = CreateService(client);

        // Act
        var report = await service.CreateReportAsync(
            [new FplPlayerPriceChange(10, "Salah", 100, 101)],
            DateTimeOffset.UtcNow,
            eventId: 3,
            CancellationToken.None);

        // Assert
        report.LeagueDataAvailable.Should().BeTrue();
        report.LeagueManagerCount.Should().Be(2);
        report.AvailableSquadCount.Should().Be(1);
        report.PlayerChanges.Single().OwnerEntryNames.Should().Equal("Bobrov FC");
    }

    [Test]
    public async Task CreateReportAsync_LargeLeague_BoundsConcurrentSquadRequests()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Managers = Enumerable
                .Range(1, 12)
                .Select(entryId => CreateManager(entryId, $"Team {entryId}"))
                .ToList(),
            BlockPickRequests = true
        };
        foreach (var manager in client.Managers)
        {
            client.Picks[manager.Entry] = CreatePicks(10);
        }

        var service = CreateService(client);

        // Act
        var reportTask = service.CreateReportAsync(
            [new FplPlayerPriceChange(10, "Salah", 100, 101)],
            DateTimeOffset.UtcNow,
            eventId: 3,
            CancellationToken.None);
        try
        {
            await client.ConcurrencyLimitReached.Task.WaitAsync(
                TimeSpan.FromSeconds(2));

            // Assert
            client.PeakConcurrentPickCalls.Should()
                .Be(FplLeaguePriceChangeService.SquadFetchConcurrencyLimit);
            reportTask.IsCompleted.Should().BeFalse();
        }
        finally
        {
            client.ReleasePickRequests();
        }

        var report = await reportTask;

        // Assert
        report.AvailableSquadCount.Should().Be(12);
    }

    [Test]
    public async Task CreateReportAsync_LeagueNotConfigured_ReturnsGlobalFallback()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient();
        var service = new FplLeaguePriceChangeService(
            client,
            new FantasyPremierLeagueOptions(),
            new RecordingLogger<FplLeaguePriceChangeService>());

        // Act
        var report = await service.CreateReportAsync(
            [new FplPlayerPriceChange(10, "Salah", 100, 101)],
            DateTimeOffset.UtcNow,
            eventId: 3,
            CancellationToken.None);

        // Assert
        report.LeagueDataAvailable.Should().BeFalse();
        report.PlayerChanges.Single().OwnerEntryNames.Should().BeEmpty();
        client.ClassicStandingsCalls.Should().Be(0);
    }

    private static FplLeaguePriceChangeService CreateService(
        TestFantasyPremierLeagueClient client)
    {
        return new FplLeaguePriceChangeService(
            client,
            new FantasyPremierLeagueOptions { ClassicLeagueId = 123 },
            new RecordingLogger<FplLeaguePriceChangeService>());
    }

    private static ClassicStanding CreateManager(int entryId, string entryName)
    {
        return new ClassicStanding
        {
            Entry = entryId,
            EntryName = entryName
        };
    }

    private static EntryEventPicksResponse CreatePicks(params int[] playerIds)
    {
        return new EntryEventPicksResponse
        {
            Picks = playerIds
                .Select(playerId => new EntryEventPick { Element = playerId })
                .ToList()
        };
    }

    private sealed class TestFantasyPremierLeagueClient : IFantasyPremierLeagueClient
    {
        private readonly TaskCompletionSource _releasePickRequests =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activePickCalls;
        private int _peakConcurrentPickCalls;

        public List<ClassicStanding> Managers { get; init; } = [];

        public Dictionary<int, EntryEventPicksResponse> Picks { get; } = [];

        public HashSet<int> PickFailures { get; } = [];

        public int ClassicStandingsCalls { get; private set; }

        public bool BlockPickRequests { get; init; }

        public int PeakConcurrentPickCalls => _peakConcurrentPickCalls;

        public TaskCompletionSource ConcurrencyLimitReached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleasePickRequests()
        {
            _releasePickRequests.TrySetResult();
        }

        public Task<ClassicStandingsResponse> GetClassicStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            ClassicStandingsCalls++;
            return Task.FromResult(new ClassicStandingsResponse
            {
                Standings = new ClassicStandings { Results = Managers }
            });
        }

        public async Task<EntryEventPicksResponse> GetEntryEventPicksAsync(
            int entryId,
            int eventId,
            CancellationToken cancellationToken)
        {
            var activeCalls = Interlocked.Increment(ref _activePickCalls);
            UpdatePeakConcurrentCalls(activeCalls);
            if (activeCalls >= FplLeaguePriceChangeService.SquadFetchConcurrencyLimit)
            {
                ConcurrencyLimitReached.TrySetResult();
            }

            try
            {
                if (BlockPickRequests)
                {
                    await _releasePickRequests.Task.WaitAsync(cancellationToken);
                }

                if (PickFailures.Contains(entryId))
                {
                    throw new InvalidOperationException("Squad unavailable.");
                }

                return Picks[entryId];
            }
            finally
            {
                Interlocked.Decrement(ref _activePickCalls);
            }
        }

        private void UpdatePeakConcurrentCalls(int activeCalls)
        {
            var currentPeak = Volatile.Read(ref _peakConcurrentPickCalls);
            while (activeCalls > currentPeak)
            {
                var observedPeak = Interlocked.CompareExchange(
                    ref _peakConcurrentPickCalls,
                    activeCalls,
                    currentPeak);
                if (observedPeak == currentPeak)
                {
                    return;
                }

                currentPeak = observedPeak;
            }
        }

        public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<HeadToHeadStandingsResponse> GetHeadToHeadStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EventLiveResponse> GetEventLiveAsync(
            int eventId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<PremierLeagueFixture>> GetFixturesAsync(
            int eventId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
