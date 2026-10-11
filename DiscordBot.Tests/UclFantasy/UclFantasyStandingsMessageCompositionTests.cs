using AwesomeAssertions;
using DiscordBot.Tests.PremierLeague;
using DiscordBot.UclFantasy;
using NUnit.Framework;

namespace DiscordBot.Tests.UclFantasy;

[TestFixture]
public sealed class UclFantasyStandingsMessageCompositionTests
{
    private UclFantasyMessageCompositionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new UclFantasyMessageCompositionService(TestTimeZones.Riga());
    }

    [Test]
    public void ComposeStandingsPage_FirstPage_ContainsLeagueIdentityRanksAndPoints()
    {
        // Arrange
        var standings = Snapshot();

        // Act
        var message = _service.ComposeStandingsPage(standings, 1);

        // Assert
        message.Should().Contain("Таблица лиги UCL");
        message.Should().Contain("Great Messi League");
        message.Should().Contain("25247707");
        message.Should().Contain("Matchday 2");
        message.Should().Contain("🥇");
        message.Should().Contain("Alpha FC");
        message.Should().Contain("200");
        message.Should().Contain("Страница 1 из");
    }

    [Test]
    public void ComposeStandingsPage_Movement_ShowsArrowsOnlyWhenSupported()
    {
        // Arrange
        var standings = Snapshot();

        // Act
        var message = _service.ComposeStandingsPage(standings, 1);

        // Assert
        message.Should().Contain("↑1");
        message.Should().Contain("↓1");
    }

    [Test]
    public void ComposeStandingsUpdate_NonPodiumMover_ListsChangedMember()
    {
        // Arrange
        var standings = Snapshot();

        // Act
        var message = _service.ComposeStandingsUpdate(standings);

        // Assert
        message.Should().StartWith("🔔 Обновление таблицы UCL:");
        message.Should().Contain("Delta Rovers");
        message.Should().Contain("Движения");
    }

    [Test]
    public void ComposeStandingsPage_EmptyStandings_ReportsExplicitly()
    {
        // Arrange
        var standings = Snapshot() with { Members = [] };

        // Act
        var message = _service.ComposeStandingsPage(standings, 1);

        // Assert
        message.Should().Contain("Данные о позициях не получены");
    }

    [Test]
    public void ComposeStandingsPage_LastPageOfDefaultFetchBound_ShowsTrailingMembers()
    {
        // Arrange: default fetch bound is 10 pages x 25 entries = 250 members,
        // displayed 10 per page => 25 display pages. Page 25 must show ranks 241-250.
        var members = Enumerable.Range(1, 250)
            .Select(index => new UclLeagueStanding(
                1000 + index,
                $"Team {index:D3}",
                $"Manager {index:D3}",
                index,
                index,
                1000 - index,
                10))
            .ToList();
        var standings = new UclLeagueStandings(
            25247707,
            "Great Messi League",
            1,
            2,
            members);

        // Act
        var message = _service.ComposeStandingsPage(standings, 25);

        // Assert
        message.Should().Contain("Team 241");
        message.Should().Contain("Team 250");
        message.Should().NotContain("Team 001");
        message.Should().Contain("Страница 25 из 25");
    }

    [Test]
    public void ComposeStandingsPage_LargerSupportedFetchBound_ShowsTrailingMembers()
    {
        // Arrange: supported larger configuration MaxStandingsPages=11,
        // PageSize=50 => 550 members, displayed 10 per page => 55 pages.
        // Page 55 must show ranks 541-550 and remain within the Discord page cap.
        var members = Enumerable.Range(1, 550)
            .Select(index => new UclLeagueStanding(
                1000 + index,
                $"Team {index:D3}",
                $"Manager {index:D3}",
                index,
                index,
                2000 - index,
                10))
            .ToList();
        var standings = new UclLeagueStandings(
            25247707,
            "Great Messi League",
            1,
            2,
            members);

        // Act
        var message = _service.ComposeStandingsPage(standings, 55);

        // Assert
        message.Should().Contain("Team 541");
        message.Should().Contain("Team 550");
        message.Should().NotContain("Team 001");
        message.Should().Contain("Страница 55 из 55");
        55.Should().BeLessThanOrEqualTo(DiscordBot.Commands.DiscordApplicationCommands.UclStandingsMaxPage);
    }

    [Test]
    public void ComposeUnavailableStandings_ReturnsRetryMessage()
    {
        // Act
        var message = UclFantasyMessageCompositionService.ComposeUnavailableStandings();

        // Assert
        message.Should().Contain("Таблица UCL сейчас недоступна");
    }

    private static UclLeagueStandings Snapshot()
    {
        return new UclLeagueStandings(
            25247707,
            "Great Messi League",
            1,
            2,
            [
                new UclLeagueStanding(101, "Alpha FC", "Anna", 1, 1, 200, 60),
                new UclLeagueStanding(102, "Beta United", "Boris", 2, 3, 190, 70),
                new UclLeagueStanding(103, "Gamma City", "Gita", 3, 2, 180, 40),
                new UclLeagueStanding(104, "Delta Rovers", "Dana", 4, 5, 170, 65)
            ]);
    }
}
