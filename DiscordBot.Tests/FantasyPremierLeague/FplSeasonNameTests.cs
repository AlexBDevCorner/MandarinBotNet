using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.Responses;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague;

[TestFixture]
public sealed class FplSeasonNameTests
{
    [Test]
    public void FromEvents_GameweeksSpanCalendarYears_UsesEarliestDeadlineYear()
    {
        // Arrange
        var events = new[]
        {
            CreateEvent(new DateTimeOffset(2027, 1, 2, 12, 0, 0, TimeSpan.Zero)),
            CreateEvent(new DateTimeOffset(2026, 8, 21, 17, 30, 0, TimeSpan.Zero))
        };

        // Act
        var season = FplSeasonName.FromEvents(events);

        // Assert
        season.Should().Be("2026/27");
    }

    [Test]
    public void FromEvents_MissingDeadlines_ThrowsInvalidDataException()
    {
        // Arrange
        var events = new[] { new PremierLeagueEvent { Id = 1 } };

        // Act
        Action act = () => FplSeasonName.FromEvents(events);

        // Assert
        act.Should().Throw<InvalidDataException>();
    }

    private static PremierLeagueEvent CreateEvent(DateTimeOffset deadline)
    {
        return new PremierLeagueEvent
        {
            DeadlineTimeEpoch = deadline.ToUnixTimeSeconds()
        };
    }
}
