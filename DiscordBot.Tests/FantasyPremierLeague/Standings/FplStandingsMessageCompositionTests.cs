using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Standings;
using DiscordBot.PremierLeague;
using DiscordBot.Tests.PremierLeague;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Standings;

[TestFixture]
public sealed class FplStandingsMessageCompositionTests
{
    private static readonly PremierLeagueMessageCompositionService Composer =
        new(TestTimeZones.Riga());

    [Test]
    public void ComposeClassicStandingsSnapshot_RendersRankNameTotalGwMovementGap()
    {
        var view = new FplClassicStandingsView(
        [
            new FplClassicStandingRow(
                1,
                "Leader",
                "Manager",
                1,
                0,
                63,
                1247,
                FplStandingsMovement.None,
                0,
                0,
                false),
            new FplClassicStandingRow(
                2,
                "Chaser",
                "Manager",
                2,
                3,
                55,
                1219,
                FplStandingsMovement.Down,
                1,
                28,
                false)
        ], TargetMarked: false);

        var message = Composer.ComposeClassicStandingsSnapshot(view);

        message.Should().Contain("🥇 Leader — 1 247 | GW 63");
        message.Should().Contain("🥈 Chaser — 1 219 | GW 55 ↓1 | 28 до лидера");
    }

    [Test]
    public void ComposeClassicStandingsSnapshot_LeaderHasNoGapSuffix()
    {
        var view = new FplClassicStandingsView(
        [
            new FplClassicStandingRow(
                1,
                "Leader",
                "Manager",
                1,
                0,
                63,
                1247,
                FplStandingsMovement.None,
                0,
                0,
                false)
        ], TargetMarked: false);

        var message = Composer.ComposeClassicStandingsSnapshot(view);

        var leaderLine = message.Split('\n')[1];
        leaderLine.Should().NotContain("до лидера");
    }

    [Test]
    public void ComposeClassicAroundStandings_MarksTargetAndUsesFocusedHeading()
    {
        var result = new FplAroundLookupResult(
            FplAroundLookupStatus.Available,
            [
                new FplClassicStandingRow(
                    6, "Team 6", "M", 6, 0, 10, 1000,
                    FplStandingsMovement.None, 0, 50, false),
                new FplClassicStandingRow(
                    8, "Target", "M", 8, 0, 10, 950,
                    FplStandingsMovement.None, 0, 100, true)
            ],
            []);
        var message = Composer.ComposeClassicAroundStandings(result, "Bob");

        message.Should().StartWith("🏆 Классическая лига — вокруг Bob");
        message.Should().Contain("👉 8. Target");
        message.Should().NotContain("👉 6. Team 6");
    }

    [Test]
    public void ComposeHeadToHeadStandingsSnapshot_RendersRowWithMatches()
    {
        var view = new FplHeadToHeadStandingsView(
        [
            new FplHeadToHeadStandingRow(
                "TeamA", 1, 0, 18, 7, FplStandingsMovement.None, 0, 0),
            new FplHeadToHeadStandingRow(
                "TeamB", 2, 3, 15, 7, FplStandingsMovement.Up, 1, 3)
        ]);

        var message = Composer.ComposeHeadToHeadStandingsSnapshot(view);

        message.Should().Contain("🥇 TeamA — 18 | 7 матчей");
        message.Should().Contain("🥈 TeamB — 15 ↑1 | 7 матчей | 3 до лидера");
    }

    [Test]
    public void ComposeHeadToHeadStandingsSnapshot_PluralizesMatches()
    {
        var view = new FplHeadToHeadStandingsView(
        [
            new FplHeadToHeadStandingRow("One", 1, 0, 10, 1, FplStandingsMovement.None, 0, 0),
            new FplHeadToHeadStandingRow("Two", 2, 0, 9, 2, FplStandingsMovement.None, 0, 0),
            new FplHeadToHeadStandingRow("Five", 3, 0, 8, 5, FplStandingsMovement.None, 0, 0)
        ]);

        var message = Composer.ComposeHeadToHeadStandingsSnapshot(view);

        message.Should().Contain("1 матч");
        message.Should().Contain("2 матча");
        message.Should().Contain("5 матчей");
    }

    [Test]
    public void ComposeClassicStandingsSnapshot_SanitizesExternalNames()
    {
        var view = new FplClassicStandingsView(
        [
            new FplClassicStandingRow(
                1, "@everyone Evil", "M", 1, 0, 10, 100,
                FplStandingsMovement.None, 0, 0, false)
        ], TargetMarked: false);

        var message = Composer.ComposeClassicStandingsSnapshot(view);

        message.Should().NotContain("@everyone");
    }

    [Test]
    public void ComposeClassicStandingsSnapshot_EmptyTable_ShowsEmptyState()
    {
        var view = new FplClassicStandingsView([], TargetMarked: false);

        var message = Composer.ComposeClassicStandingsSnapshot(view);

        message.Should().Contain("🤷 Данные о позициях не получены.");
    }

    [Test]
    public void ComposeManagerNotFound_ReturnsFriendlyMessage()
    {
        var message = Composer.ComposeManagerNotFound("Bob");

        message.Should().Be(
            "🤷 Не удалось найти менеджера или команду \"Bob\".");
    }

    [Test]
    public void ComposeAmbiguousManager_ListsCandidates()
    {
        var candidates = new[]
        {
            new FplClassicCandidate("Alex FC", "Alex"),
            new FplClassicCandidate("FC Alexander", "Alexander")
        };

        var message = Composer.ComposeAmbiguousManager("Alex", candidates);

        message.Should().StartWith(
            "🤔 Нашлось несколько вариантов для \"Alex\":");
        message.Should().Contain("Alex FC");
        message.Should().Contain("FC Alexander");
        message.Should().Contain("Уточните название команды или имя менеджера.");
    }
}
