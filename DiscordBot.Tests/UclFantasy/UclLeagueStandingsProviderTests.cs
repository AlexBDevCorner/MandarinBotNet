using AwesomeAssertions;
using DiscordBot.UclFantasy;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace DiscordBot.Tests.UclFantasy;

[TestFixture]
public sealed class UclLeagueStandingsProviderTests
{
    private static readonly UclFantasyOptions Options = new()
    {
        LeagueId = 25247707,
        MaxStandingsPages = 10,
        PageSize = 25,
        OptType = 1,
        PhaseId = 1,
        MatchdayId = 0
    };

    [Test]
    public async Task GetCurrentStandingsAsync_AutoDetect_UsesCurrentMatchday()
    {
        // Arrange
        var client = new TestUclFantasyClient
        {
            Matchdays =
            [
                new UclFantasyMatchday { MatchdayId = 1, IsCurrent = 0 },
                new UclFantasyMatchday { MatchdayId = 2, IsCurrent = 1 },
                new UclFantasyMatchday { MatchdayId = 3, IsCurrent = 0 }
            ]
        };
        var provider = CreateProvider(client);

        // Act
        await provider.GetCurrentStandingsAsync(CancellationToken.None);

        // Assert
        client.RequestedQuery.Should().NotBeNull();
        client.RequestedQuery!.MatchdayId.Should().Be(2);
        client.RequestedQuery.PhaseId.Should().Be(1);
        client.RequestedQuery.OptType.Should().Be(1);
        client.RequestedQuery.LeagueId.Should().Be(25247707);
    }

    [Test]
    public async Task GetCurrentStandingsAsync_PinnedMatchday_SkipsFixturesFeed()
    {
        // Arrange
        var client = new TestUclFantasyClient();
        var pinned = new UclFantasyOptions
        {
            LeagueId = 25247707,
            MaxStandingsPages = 10,
            PageSize = 25,
            OptType = 1,
            PhaseId = 1,
            MatchdayId = 5
        };
        var provider = new UclLeagueStandingsProvider(
            client,
            pinned,
            NullLogger<UclLeagueStandingsProvider>.Instance);

        // Act
        await provider.GetCurrentStandingsAsync(CancellationToken.None);

        // Assert
        client.WebConfigurationCalls.Should().Be(0);
        client.FixturesCalls.Should().Be(0);
        client.RequestedQuery.Should().NotBeNull();
        client.RequestedQuery!.MatchdayId.Should().Be(5);
    }

    [Test]
    public async Task GetCurrentStandingsAsync_NoCurrentFlag_FallsBackToHighestMatchday()
    {
        // Arrange
        var client = new TestUclFantasyClient
        {
            Matchdays =
            [
                new UclFantasyMatchday { MatchdayId = 1, IsCurrent = 0 },
                new UclFantasyMatchday { MatchdayId = 4, IsCurrent = 0 }
            ]
        };
        var provider = CreateProvider(client);

        // Act
        await provider.GetCurrentStandingsAsync(CancellationToken.None);

        // Assert
        client.RequestedQuery.Should().NotBeNull();
        client.RequestedQuery!.MatchdayId.Should().Be(4);
    }

    private static UclLeagueStandingsProvider CreateProvider(TestUclFantasyClient client)
    {
        return new UclLeagueStandingsProvider(
            client,
            Options,
            NullLogger<UclLeagueStandingsProvider>.Instance);
    }

    private sealed class TestUclFantasyClient : IUclFantasyClient
    {
        public List<UclFantasyMatchday> Matchdays { get; init; } =
        [
            new UclFantasyMatchday { MatchdayId = 2, IsCurrent = 1 }
        ];

        public int WebConfigurationCalls { get; private set; }

        public int FixturesCalls { get; private set; }

        public UclLeagueStandingsQuery? RequestedQuery { get; private set; }

        public Task<UclFantasyWebConfigurationResponse> GetWebConfigurationAsync(
            CancellationToken cancellationToken)
        {
            WebConfigurationCalls++;
            return Task.FromResult(new UclFantasyWebConfigurationResponse
            {
                Data = new UclFantasyWebConfigurationData
                {
                    Value = new UclFantasyWebConfigurationValue { TourId = 90 }
                }
            });
        }

        public Task<UclFantasyFixturesResponse> GetFixturesAsync(
            UclFantasyWebConfigurationValue webConfiguration,
            CancellationToken cancellationToken)
        {
            FixturesCalls++;
            return Task.FromResult(new UclFantasyFixturesResponse
            {
                Data = new UclFantasyFixturesData { Value = Matchdays }
            });
        }

        public Task<UclLeagueStandings> GetLeagueStandingsAsync(
            UclLeagueStandingsQuery query,
            CancellationToken cancellationToken)
        {
            RequestedQuery = query;
            return Task.FromResult(new UclLeagueStandings(
                query.LeagueId,
                "Test League",
                query.PhaseId,
                query.MatchdayId,
                [new UclLeagueStanding(1, "Alpha FC", "Anna", 1, 1, 100, 10)]));
        }
    }
}
