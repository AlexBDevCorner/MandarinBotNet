using AwesomeAssertions;
using DiscordBot;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using DiscordBot.Responses;
using NUnit.Framework;

namespace DiscordBot.Tests.PremierLeague;

[TestFixture]
public sealed class DeadlineSelectionServiceTests
{
    [Test]
    public void SelectNext_NextEventExists_ReturnsItsUtcDeadline()
    {
        // Arrange
        var expectedDeadline = new DateTimeOffset(
            2027,
            2,
            2,
            12,
            0,
            0,
            TimeSpan.Zero);
        var events = new[]
        {
            new PremierLeagueEvent { Id = 41, IsNext = false },
            new PremierLeagueEvent
            {
                Id = 42,
                IsNext = true,
                DeadlineTimeEpoch = expectedDeadline.ToUnixTimeSeconds()
            }
        };

        // Act
        var result = new DeadlineSelectionService().SelectNext(events);

        // Assert
        result.Should().Be(new PremierLeagueDeadline(42, expectedDeadline));
    }

    [Test]
    public void SelectNext_NoNextEvent_ReturnsNull()
    {
        // Arrange
        var events = new[] { new PremierLeagueEvent { Id = 41, IsNext = false } };

        // Act
        var result = new DeadlineSelectionService().SelectNext(events);

        // Assert
        result.Should().BeNull();
    }
}

[TestFixture]
public sealed class ReminderEligibilityServiceTests
{
    [TestCase(1497, NotificationTypes.Deadline24Hours)]
    [TestCase(1383, NotificationTypes.Deadline24Hours)]
    [TestCase(69, NotificationTypes.Deadline1Hour)]
    [TestCase(1, NotificationTypes.Deadline1Hour)]
    [TestCase(1498, null)]
    [TestCase(1382, null)]
    [TestCase(70, null)]
    [TestCase(0, null)]
    [TestCase(-1, null)]
    public void SelectNotificationType_RemainingTime_ReturnsExpectedType(
        int remainingMinutes,
        string? expected)
    {
        // Arrange
        var deadline = new DateTimeOffset(
            2027,
            2,
            2,
            12,
            0,
            0,
            TimeSpan.Zero);
        var now = deadline.AddMinutes(-remainingMinutes);

        // Act
        var result = new ReminderEligibilityService()
            .SelectNotificationType(now, deadline);

        // Assert
        result.Should().Be(expected);
    }

    [Test]
    public void SelectNotificationType_CustomNotificationTypes_UsesProvidedPrefix()
    {
        // Arrange
        var deadline = new DateTimeOffset(
            2027,
            2,
            2,
            12,
            0,
            0,
            TimeSpan.Zero);
        var now = deadline.AddMinutes(-60);

        // Act
        var result = new ReminderEligibilityService().SelectNotificationType(
            now,
            deadline,
            NotificationTypes.UclDeadline24Hours,
            NotificationTypes.UclDeadline1Hour);

        // Assert
        result.Should().Be(NotificationTypes.UclDeadline1Hour);
    }
}

[TestFixture]
public sealed class StandingsPublicationEligibilityServiceTests
{
    [Test]
    public void IsUpdatedSinceYesterday_DstBoundary_UsesRigaMidnightAsUtcInstant()
    {
        // Arrange
        var now = new DateTimeOffset(
            2027,
            3,
            29,
            10,
            0,
            0,
            TimeSpan.Zero);
        var startOfYesterdayUtc = new DateTimeOffset(
            2027,
            3,
            27,
            22,
            0,
            0,
            TimeSpan.Zero);
        var service = new StandingsPublicationEligibilityService(
            TestTimeZones.Riga());

        // Act
        var atBoundary = service.IsUpdatedSinceYesterday(startOfYesterdayUtc, now);
        var beforeBoundary = service.IsUpdatedSinceYesterday(
            startOfYesterdayUtc.AddTicks(-1),
            now);

        // Assert
        atBoundary.Should().BeTrue();
        beforeBoundary.Should().BeFalse();
    }
}

[TestFixture]
public sealed class StandingsChangeServiceTests
{
    [Test]
    public void GetChanges_MixedMovement_ReturnsOnlyChangedTeams()
    {
        // Arrange
        var standings = new[]
        {
            CreateClassicStanding("Up", rank: 1, lastRank: 3),
            CreateClassicStanding("Same", rank: 2, lastRank: 2),
            CreateClassicStanding("Down", rank: 4, lastRank: 1)
        };

        // Act
        var result = new StandingsChangeService().GetChanges(standings);

        // Assert
        result.Should().BeEquivalentTo(
            [
                new StandingsChange("Up", 2, StandingsChangeDirection.Up),
                new StandingsChange("Down", 3, StandingsChangeDirection.Down)
            ],
            options => options.WithStrictOrdering());
    }

    private static ClassicStanding CreateClassicStanding(
        string name,
        int rank,
        int lastRank)
    {
        return new ClassicStanding
        {
            EntryName = name,
            Rank = rank,
            LastRank = lastRank
        };
    }
}

[TestFixture]
public sealed class WinnerSelectionServiceTests
{
    [Test]
    public void SelectEventWinners_MultipleTeams_ReturnsHighestEventTotal()
    {
        // Arrange
        var standings = new[]
        {
            new ClassicStanding { EntryName = "First", EventTotal = 55 },
            new ClassicStanding { EntryName = "Winner", EventTotal = 72 },
            new ClassicStanding { EntryName = "Third", EventTotal = 48 }
        };

        // Act
        var result = new WinnerSelectionService().SelectEventWinners(standings);

        // Assert
        result.Should().ContainSingle().Which.Should().BeSameAs(standings[1]);
    }

    [Test]
    public void SelectEventWinners_TiedTeams_ReturnsEveryWinnerDeterministically()
    {
        // Arrange
        var standings = new[]
        {
            new ClassicStanding
            {
                Entry = 30,
                EntryName = "Zulu",
                Rank = 3,
                EventTotal = 72
            },
            new ClassicStanding
            {
                Entry = 20,
                EntryName = "Beta",
                Rank = 2,
                EventTotal = 72
            },
            new ClassicStanding
            {
                Entry = 10,
                EntryName = "Alpha",
                Rank = 2,
                EventTotal = 72
            }
        };

        // Act
        var result = new WinnerSelectionService().SelectEventWinners(standings);

        // Assert
        result.Select(winner => winner.EntryName).Should().BeEquivalentTo(
            ["Alpha", "Beta", "Zulu"],
            options => options.WithStrictOrdering());
    }

    [Test]
    public void SelectEventWinners_NoTeams_ReturnsEmptyCollection()
    {
        // Arrange
        var standings = Array.Empty<ClassicStanding>();

        // Act
        var result = new WinnerSelectionService().SelectEventWinners(standings);

        // Assert
        result.Should().BeEmpty();
    }
}

[TestFixture]
public sealed class PremierLeagueMessageCompositionServiceTests
{
    private PremierLeagueMessageCompositionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new PremierLeagueMessageCompositionService(
            TestTimeZones.Riga());
    }

    [Test]
    public void ComposeDeadlineReminder_WinterDeadline_FormatsRigaTimeAndRussianDuration()
    {
        // Arrange
        var deadline = new DateTimeOffset(
            2027,
            2,
            2,
            12,
            0,
            0,
            TimeSpan.Zero);

        // Act
        var result = _service.ComposeDeadlineReminder(
            deadline,
            deadline.AddDays(-1));

        // Assert
        result.Should().Contain("02 февраля 2027, 14:00");
        result.Should().Contain("это вторник");
        result.Should().EndWith("осталось всего 1 день, 0 часов, 0 минут.");
    }

    [Test]
    public void ComposeClassicStandings_KnownVariantAndChanges_PreservesRussianMessage()
    {
        // Arrange
        var winner = new ClassicStanding
        {
            EntryName = "Пельмени",
            Rank = 1,
            LastRank = 3,
            Total = 100,
            EventTotal = 72
        };
        var standings = new[]
        {
            winner,
            new ClassicStanding
            {
                EntryName = "Обнимашки",
                Rank = 4,
                LastRank = 2,
                Total = 80,
                EventTotal = 40
            }
        };
        var changes = new StandingsChangeService().GetChanges(standings);

        // Act
        var result = _service.ComposeClassicStandings(
            standings,
            [winner],
            changes,
            congratulationsVariant: 0);

        // Assert
        result.Should().StartWith(
            "🏆 Лига Пельменных Обнимашек:\n:one: Пельмени 100\n4. Обнимашки 80");
        result.Should().Contain(
            "В последнем туре больше всех баллов набрала команда Пельмени - 72");
        result.Should().Contain(
            "Команда Пельмени смогла взобраться на 2 позиции вверх");
        result.Should().Contain(
            "Команда Обнимашки упала на 2 позиции вниз");
    }

    [Test]
    public void ComposeHeadToHeadStandings_Results_PreservesRankFormatting()
    {
        // Arrange
        var standings = new[]
        {
            new HeadToHeadStanding { Rank = 1, EntryName = "A", Total = 9 },
            new HeadToHeadStanding { Rank = 12, EntryName = "B", Total = 6 }
        };

        // Act
        var result = _service.ComposeHeadToHeadStandings(standings);

        // Assert
        result.Should().Be(
            "⚔️ Лига Пельменных Обнимашек-К-Обнимашкам:\n:one: A 9\n12. B 6");
    }

    [Test]
    public void ComposeClassicStandings_TiedWinners_NamesEveryWinnerInOrder()
    {
        // Arrange
        var winners = new[]
        {
            new ClassicStanding { EntryName = "Alpha", Rank = 1, EventTotal = 70 },
            new ClassicStanding { EntryName = "Beta", Rank = 2, EventTotal = 70 }
        };

        // Act
        var result = _service.ComposeClassicStandings(
            winners,
            winners,
            [],
            congratulationsVariant: 0);

        // Assert
        result.Should().Contain(
            "максимум очков (70) разделили команды: Alpha, Beta");
    }

    [Test]
    public void ComposeClassicStandings_ExternalMentionAndLineBreak_SanitizesEveryName()
    {
        // Arrange
        var standing = new ClassicStanding
        {
            EntryName = "@everyone\nInjected",
            Rank = 1,
            LastRank = 2,
            EventTotal = 70
        };

        // Act
        var result = _service.ComposeClassicStandings(
            [standing],
            [standing],
            new StandingsChangeService().GetChanges([standing]),
            congratulationsVariant: 30);

        // Assert
        result.Should().NotContain("@everyone");
        result.Should().Contain("@\u200Beveryone Injected");
    }
}

internal static class TestTimeZones
{
    public static ConfiguredTimeZone Riga()
    {
        return new ConfiguredTimeZone(
            new JobSchedulesOptions { TimeZoneId = "Europe/Riga" });
    }
}
