using AwesomeAssertions;
using DiscordBot.Deadlines;
using DiscordBot.UclFantasy;
using NUnit.Framework;

namespace DiscordBot.Tests.Deadlines;

[TestFixture]
public sealed class UclFantasyDeadlineProviderTests
{
    [Test]
    public async Task GetNextAsync_CurrentUnlockedMatchday_ParsesCentralEuropeanDeadline()
    {
        // Arrange
        var client = new TestUclFantasyClient
        {
            WebConfiguration = CreateWebConfiguration(90),
            Fixtures = CreateFixtures(
                new UclFantasyMatchday
                {
                    MatchdayId = 1,
                    IsCurrent = 1,
                    IsLocked = 0,
                    Deadline = "09/08/26 06:45:00 PM"
                })
        };
        var provider = CreateProvider(client);

        // Act
        var result = await provider.GetNextAsync(CancellationToken.None);

        // Assert
        result.Should().Be(new CompetitionDeadline(
            "UCL",
            "Matchday",
            1,
            new DateTimeOffset(2026, 9, 8, 16, 45, 0, TimeSpan.Zero)));
        client.RequestedTourId.Should().Be(90);
    }

    [Test]
    public async Task GetNextAsync_LockedCurrentMatchday_SelectsNextUnlockedFutureMatchday()
    {
        // Arrange
        var client = new TestUclFantasyClient
        {
            WebConfiguration = CreateWebConfiguration(90),
            Fixtures = CreateFixtures(
                new UclFantasyMatchday
                {
                    MatchdayId = 1,
                    IsCurrent = 1,
                    IsLocked = 1,
                    Deadline = "09/08/26 06:45:00 PM"
                },
                new UclFantasyMatchday
                {
                    MatchdayId = 2,
                    IsCurrent = 0,
                    IsLocked = 0,
                    Deadline = "09/29/26 06:45:00 PM"
                })
        };
        var provider = CreateProvider(client);

        // Act
        var result = await provider.GetNextAsync(CancellationToken.None);

        // Assert
        result!.RoundNumber.Should().Be(2);
        result.DeadlineUtc.Should().Be(
            new DateTimeOffset(2026, 9, 29, 16, 45, 0, TimeSpan.Zero));
    }

    [Test]
    public async Task GetNextAsync_NoUnlockedFutureMatchday_ReturnsNull()
    {
        // Arrange
        var client = new TestUclFantasyClient
        {
            WebConfiguration = CreateWebConfiguration(90),
            Fixtures = CreateFixtures(
                new UclFantasyMatchday
                {
                    MatchdayId = 1,
                    IsCurrent = 1,
                    IsLocked = 1,
                    Deadline = "09/08/26 06:45:00 PM"
                })
        };
        var provider = CreateProvider(client);

        // Act
        var result = await provider.GetNextAsync(CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    private static UclFantasyDeadlineProvider CreateProvider(
        TestUclFantasyClient client)
    {
        return new UclFantasyDeadlineProvider(
            client,
            new FixedTimeProvider(
                new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero)),
            new RecordingLogger<UclFantasyDeadlineProvider>());
    }

    private static UclFantasyWebConfigurationResponse CreateWebConfiguration(
        int tourId)
    {
        return new UclFantasyWebConfigurationResponse
        {
            Data = new UclFantasyWebConfigurationData
            {
                Value = new UclFantasyWebConfigurationValue
                {
                    TourId = tourId
                }
            }
        };
    }

    private static UclFantasyFixturesResponse CreateFixtures(
        params UclFantasyMatchday[] matchdays)
    {
        return new UclFantasyFixturesResponse
        {
            Data = new UclFantasyFixturesData
            {
                Value = matchdays
            }
        };
    }

    private sealed class TestUclFantasyClient : IUclFantasyClient
    {
        public required UclFantasyWebConfigurationResponse WebConfiguration { get; init; }

        public required UclFantasyFixturesResponse Fixtures { get; init; }

        public int RequestedTourId { get; private set; }

        public Task<UclFantasyWebConfigurationResponse> GetWebConfigurationAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(WebConfiguration);
        }

        public Task<UclFantasyFixturesResponse> GetFixturesAsync(
            UclFantasyWebConfigurationValue webConfiguration,
            CancellationToken cancellationToken)
        {
            RequestedTourId = webConfiguration.TourId;
            return Task.FromResult(Fixtures);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
