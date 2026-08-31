using AwesomeAssertions;
using DiscordBot;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Chips;
using DiscordBot.FantasyPremierLeague.ChipWatch;
using DiscordBot.PremierLeague;
using DiscordBot.Responses;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.ChipWatch;

[TestFixture]
public sealed class FplChipWatchServiceTests
{
    [Test]
    public async Task CreateReportAsync_EntryMetadataFails_WcFhTreatedAsUnavailable()
    {
        // Arrange
        var bootstrap = CreateBootstrap(targetEventId: 8, finalEventId: 38);
        var fixtures = new List<PremierLeagueFixture>
        {
            new() { Id = 1, EventId = 8, HomeTeamId = 1, AwayTeamId = 2, HomeTeamDifficulty = 2, AwayTeamDifficulty = 2 }
        };
        var standings = new ClassicStandingsResponse
        {
            Standings = new ClassicStandings
            {
                Results = [new ClassicStanding { Entry = 123, EntryName = "TeamA", PlayerName = "PlayerA", Rank = 1, LastRank = 1, Total = 100, EventTotal = 50 }]
            },
            LastUpdatedData = DateTimeOffset.UtcNow
        };
        var client = new TestClient(bootstrap, fixtures, standings, entryShouldThrow: true);
        var service = CreateService(client);

        // Act
        var report = await service.CreateReportAsync(CancellationToken.None);

        // Assert
        report.Should().NotBeNull();
        var manager = report!.Managers.Should().ContainSingle().Subject;
        manager.ChipHistoryAvailable.Should().BeTrue();
        var wc = manager.Chips.First(c => c.Chip == FplChipType.Wildcard);
        wc.IsAvailable.Should().BeFalse();
        wc.UnavailabilityReason.Should().Be(FplChipUnavailabilityReason.EntryMetadataUnavailable);
        var fh = manager.Chips.First(c => c.Chip == FplChipType.FreeHit);
        fh.IsAvailable.Should().BeFalse();
        fh.UnavailabilityReason.Should().Be(FplChipUnavailabilityReason.EntryMetadataUnavailable);
        manager.FreeHitRecommendation.Should().BeNull();
        // BB/TC should still be available (no history)
        manager.Chips.First(c => c.Chip == FplChipType.BenchBoost).IsAvailable.Should().BeTrue();
        manager.Chips.First(c => c.Chip == FplChipType.TripleCaptain).IsAvailable.Should().BeTrue();
    }

    [Test]
    public async Task GetUpcomingContextAsync_OutsideWindow_DoesNotFetchFixtures()
    {
        // Arrange
        var bootstrap = CreateBootstrap(targetEventId: 12, finalEventId: 38, deadlineHoursFromNow: 100); // 100h >36
        var client = new TestClient(bootstrap, [], new ClassicStandingsResponse { Standings = new ClassicStandings { Results = [] } });
        var service = CreateService(client, now: new DateTimeOffset(2027, 8, 1, 10, 0, 0, TimeSpan.Zero));

        // Act
        var context = await service.GetUpcomingContextAsync(CancellationToken.None);

        // Assert
        context.Should().NotBeNull();
        context!.TargetEventId.Should().Be(12);
        client.FixturesFetched.Should().BeFalse();
        client.StandingsFetched.Should().BeFalse();
    }

    private static BootstrapStaticResponse CreateBootstrap(int targetEventId, int finalEventId, int deadlineHoursFromNow = 20)
    {
        var now = DateTimeOffset.UtcNow;
        var events = Enumerable.Range(1, finalEventId).Select(id => new PremierLeagueEvent
        {
            Id = id,
            IsNext = id == targetEventId,
            DeadlineTimeEpoch = (id == targetEventId ? now.AddHours(deadlineHoursFromNow) : now.AddHours(-24 * (targetEventId - id))).ToUnixTimeSeconds()
        }).ToList();
        return new BootstrapStaticResponse
        {
            Events = events,
            Elements = [new PremierLeagueElement { Id = 1, TeamId = 1, Status = "a", WebName = "P1" }]
        };
    }

    private static FplChipWatchService CreateService(TestClient client, DateTimeOffset? now = null)
    {
        var timeProvider = new TestTimeProvider(now ?? new DateTimeOffset(2027, 8, 1, 10, 0, 0, TimeSpan.Zero));
        return new FplChipWatchService(
            client,
            new FantasyPremierLeagueOptions { ClassicLeagueId = 123 },
            new DeadlineSelectionService(),
            new FplChipUsageService(new FplChipSeasonRules(), NullLogger<FplChipUsageService>.Instance),
            new FplFreeHitOpportunityService(),
            timeProvider,
            NullLogger<FplChipWatchService>.Instance);
    }

    private sealed class TestClient : IFantasyPremierLeagueClient
    {
        private readonly BootstrapStaticResponse _bootstrap;
        private readonly IReadOnlyList<PremierLeagueFixture> _fixtures;
        private readonly ClassicStandingsResponse _standings;
        private readonly bool _entryShouldThrow;

        public bool FixturesFetched { get; private set; }
        public bool StandingsFetched { get; private set; }

        public TestClient(BootstrapStaticResponse bootstrap, IReadOnlyList<PremierLeagueFixture> fixtures, ClassicStandingsResponse standings, bool entryShouldThrow = false)
        {
            _bootstrap = bootstrap;
            _fixtures = fixtures;
            _standings = standings;
            _entryShouldThrow = entryShouldThrow;
        }

        public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(CancellationToken cancellationToken) => Task.FromResult(_bootstrap);
        public Task<ClassicStandingsResponse> GetClassicStandingsAsync(int leagueId, CancellationToken cancellationToken)
        {
            StandingsFetched = true;
            return Task.FromResult(_standings);
        }
        public Task<HeadToHeadStandingsResponse> GetHeadToHeadStandingsAsync(int leagueId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EntryEventPicksResponse> GetEntryEventPicksAsync(int entryId, int eventId, CancellationToken cancellationToken) =>
            Task.FromResult(new EntryEventPicksResponse { Picks = [new EntryEventPick { Element = 1 }], EntryHistory = null, AutomaticSubstitutions = [] });
        public Task<EventLiveResponse> GetEventLiveAsync(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PremierLeagueFixture>> GetFixturesAsync(int eventId, CancellationToken cancellationToken)
        {
            FixturesFetched = true;
            if (_fixtures.Count == 0)
            {
                throw new FantasyPremierLeagueApiException(FantasyPremierLeagueFailureKind.InvalidPayload, "empty");
            }
            return Task.FromResult(_fixtures);
        }
        public Task<EntryHistoryResponse> GetEntryHistoryAsync(int entryId, CancellationToken cancellationToken) =>
            Task.FromResult(new EntryHistoryResponse { Current = [], Chips = [] });
        public Task<EntryResponse> GetEntryAsync(int entryId, CancellationToken cancellationToken) =>
            _entryShouldThrow ? throw new InvalidOperationException("entry failed") : Task.FromResult(new EntryResponse { Id = entryId, StartedEvent = 5 });
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
