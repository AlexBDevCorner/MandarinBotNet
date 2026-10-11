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
