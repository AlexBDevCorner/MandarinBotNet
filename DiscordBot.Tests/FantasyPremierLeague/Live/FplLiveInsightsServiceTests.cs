using System.Net;
using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Live;
using DiscordBot.Responses;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Live;

[TestFixture]
public sealed class FplLiveInsightsServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 21, 18, 50, 0, TimeSpan.Zero);

    [Test]
    public async Task GetCurrentAsync_ActiveGameweek_UsesConfiguredLeagueAndReturnsInsights()
    {
        // Arrange
        var client = CreateClient();
        var options = CreateOptions();
        var service = CreateService(client, options);

        // Act
        var result = await service.GetCurrentAsync(CancellationToken.None);

        // Assert
        result.Availability.Should().Be(FplLiveInsightsAvailability.Available);
        result.Gameweek.Should().NotBeNull();
        result.Gameweek!.EventId.Should().Be(5);
        result.Gameweek.Managers.Should().ContainSingle().Which.EntryName.Should()
            .Be("Configured Team");
        client.Operations.Should().Equal(
            "Bootstrap",
            "Classic:456",
            "Live:5",
            "Picks:123:5");
    }

    [Test]
    public async Task GetCurrentAsync_StaleStandings_SkipsLiveRequestsAndReturnsFreshnessMetadata()
    {
        // Arrange
        var client = CreateClient();
        client.Standings = CreateStandings(
            new DateTimeOffset(2026, 8, 21, 17, 0, 0, TimeSpan.Zero));
        var service = CreateService(client, CreateOptions());

        // Act
        var result = await service.GetCurrentAsync(CancellationToken.None);

        // Assert
        result.Availability.Should().Be(FplLiveInsightsAvailability.Stale);
        result.Gameweek.Should().NotBeNull();
        result.Gameweek!.EventId.Should().Be(5);
        result.Gameweek.SourceUpdatedAtUtc.Should().Be(
            new DateTimeOffset(2026, 8, 21, 17, 0, 0, TimeSpan.Zero));
        client.Operations.Should().Equal("Bootstrap", "Classic:456");
    }

    [Test]
    public async Task GetCurrentAsync_TransientUpstreamFailure_ReturnsSafeUnavailableResult()
    {
        // Arrange
        var client = CreateClient();
        client.BootstrapException = new FantasyPremierLeagueApiException(
            FantasyPremierLeagueFailureKind.Transient,
            "Rate limited.",
            HttpStatusCode.TooManyRequests);
        var logger = new RecordingLogger<FplLiveInsightsService>();
        var service = CreateService(client, CreateOptions(), logger);

        // Act
        var result = await service.GetCurrentAsync(CancellationToken.None);

        // Assert
        result.Availability.Should().Be(FplLiveInsightsAvailability.Unavailable);
        result.FailureKind.Should().Be(FantasyPremierLeagueFailureKind.Transient);
        logger.Entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Warning);
    }

    private static FplLiveInsightsService CreateService(
        TestFantasyPremierLeagueClient client,
        FantasyPremierLeagueOptions options,
        ILogger<FplLiveInsightsService>? logger = null)
    {
        return new FplLiveInsightsService(
            client,
            options,
            new FplLiveInsightsCalculationService(options),
            new FixedTimeProvider(),
            logger ?? new RecordingLogger<FplLiveInsightsService>());
    }

    private static FantasyPremierLeagueOptions CreateOptions()
    {
        return new FantasyPremierLeagueOptions
        {
            ClassicLeagueId = 456,
            LiveDataMaxAge = TimeSpan.FromMinutes(20)
        };
    }

    private static TestFantasyPremierLeagueClient CreateClient()
    {
        return new TestFantasyPremierLeagueClient
        {
            Standings = CreateStandings(
                new DateTimeOffset(2026, 8, 21, 18, 45, 0, TimeSpan.Zero))
        };
    }

    private static ClassicStandingsResponse CreateStandings(
        DateTimeOffset lastUpdatedData)
    {
        return new ClassicStandingsResponse
        {
            LastUpdatedData = lastUpdatedData,
            Standings = new ClassicStandings
            {
                Results =
                [
                    new ClassicStanding
                    {
                        Entry = 123,
                        EntryName = "Configured Team",
                        PlayerName = "Configured Manager",
                        Rank = 1
                    }
                ]
            }
        };
    }

    private sealed class TestFantasyPremierLeagueClient : IFantasyPremierLeagueClient
    {
        public List<string> Operations { get; } = [];

        public ClassicStandingsResponse Standings { get; set; } = null!;

        public Exception? BootstrapException { get; set; }

        public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
            CancellationToken cancellationToken)
        {
            Operations.Add("Bootstrap");
            return BootstrapException is null
                ? Task.FromResult(new BootstrapStaticResponse
                {
                    SeasonName = "2026/27",
                    Events =
                    [
                        new PremierLeagueEvent { Id = 4, IsFinished = true },
                        new PremierLeagueEvent { Id = 5, IsCurrent = true }
                    ],
                    Elements =
                    [
                        new PremierLeagueElement { Id = 1, WebName = "Captain" },
                        new PremierLeagueElement { Id = 2, WebName = "Vice" },
                        new PremierLeagueElement { Id = 3, WebName = "Bench" }
                    ]
                })
                : Task.FromException<BootstrapStaticResponse>(BootstrapException);
        }

        public Task<ClassicStandingsResponse> GetClassicStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            Operations.Add($"Classic:{leagueId}");
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
            Operations.Add($"Picks:{entryId}:{eventId}");
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
                        Position = 2,
                        Multiplier = 1,
                        IsViceCaptain = true
                    },
                    new EntryEventPick
                    {
                        Element = 3,
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
            Operations.Add($"Live:{eventId}");
            return Task.FromResult(new EventLiveResponse
            {
                Elements =
                [
                    new EventLiveElement
                    {
                        Id = 1,
                        Stats = new EventLiveElementStats
                        {
                            TotalPoints = 10,
                            Minutes = 90
                        }
                    },
                    new EventLiveElement
                    {
                        Id = 2,
                        Stats = new EventLiveElementStats
                        {
                            TotalPoints = 4,
                            Minutes = 0
                        }
                    },
                    new EventLiveElement
                    {
                        Id = 3,
                        Stats = new EventLiveElementStats
                        {
                            TotalPoints = 8,
                            Minutes = 90
                        }
                    }
                ]
            });
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
