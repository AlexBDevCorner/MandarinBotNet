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
            DateTimeOffset.Parse("2026-08-30T10:00:00Z"),
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
        public List<ClassicStanding> Managers { get; init; } = [];

        public Dictionary<int, EntryEventPicksResponse> Picks { get; } = [];

        public HashSet<int> PickFailures { get; } = [];

        public int ClassicStandingsCalls { get; private set; }

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

        public Task<EntryEventPicksResponse> GetEntryEventPicksAsync(
            int entryId,
            int eventId,
            CancellationToken cancellationToken)
        {
            return PickFailures.Contains(entryId)
                ? Task.FromException<EntryEventPicksResponse>(
                    new InvalidOperationException("Squad unavailable."))
                : Task.FromResult(Picks[entryId]);
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
