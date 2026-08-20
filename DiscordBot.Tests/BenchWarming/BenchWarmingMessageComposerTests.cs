using AwesomeAssertions;
using DiscordBot.BenchWarming;
using NUnit.Framework;

namespace DiscordBot.Tests.BenchWarming;

[TestFixture]
public sealed class BenchWarmingMessageComposerTests
{
    private readonly BenchWarmingMessageComposer _composer = new();

    [Test]
    public void ComposeRoundSummary_WithStandings_IncludesTopRoundAndSeasonTables()
    {
        // Arrange
        var round = new BenchWarmingRoundResult(
            "2026/27",
            5,
            WasAlreadyCalculated: false,
            BenchPoints:
            [
                new BenchWarmingPlayerPoints(100, "Team A", 1, "Haaland", 15),
                new BenchWarmingPlayerPoints(100, "Team A", 2, "Kepa", 2),
                new BenchWarmingPlayerPoints(200, "Team B", 3, "Salah", 7)
            ],
            RoundStandings:
            [
                new BenchWarmingEntryStanding(100, "Team A", 17),
                new BenchWarmingEntryStanding(200, "Team B", 7)
            ],
            SeasonStandings:
            [
                new BenchWarmingEntryStanding(100, "Team A", 17),
                new BenchWarmingEntryStanding(200, "Team B", 7)
            ]);

        // Act
        var message = _composer.ComposeRoundSummary(round);

        // Assert
        message.Should().Be(
            "Лига Обогревателей Скамейки — итоги тура 5 (сезон 2026/27):" +
            "\n\nБольше всех очков оставили на скамейке в этом туре:" +
            "\n:one: Team A — 17" +
            "\n:two: Team B — 7" +
            "\n\nГлавный обогреватель скамейки тура: Haaland — 15 очков, греющих лавку команды Team A!" +
            "\n\nОбщий зачёт сезона 2026/27:" +
            "\n:one: Team A — 17" +
            "\n:two: Team B — 7");
    }

    [Test]
    public void ComposeRoundSummary_NoBenchPoints_ShowsOnlySeasonTable()
    {
        // Arrange
        var round = new BenchWarmingRoundResult(
            "2026/27",
            5,
            WasAlreadyCalculated: false,
            BenchPoints: [],
            RoundStandings: [],
            SeasonStandings: []);

        // Act
        var message = _composer.ComposeRoundSummary(round);

        // Assert
        message.Should().Be(
            "Лига Обогревателей Скамейки — итоги тура 5 (сезон 2026/27):" +
            "\n\nОбщий зачёт сезона 2026/27:");
    }

    [Test]
    public void ComposeSeasonStandings_WithStandings_ListsEntriesWithRankLabels()
    {
        // Arrange
        var standings = new[]
        {
            new BenchWarmingEntryStanding(100, "Team A", 42),
            new BenchWarmingEntryStanding(200, "Team B", 20),
            new BenchWarmingEntryStanding(300, "Team C", 4),
            new BenchWarmingEntryStanding(400, "Team D", 1)
        };

        // Act
        var message = _composer.ComposeSeasonStandings("2026/27", standings);

        // Assert
        message.Should().Be(
            "Лига Обогревателей Скамейки (сезон 2026/27):" +
            "\n:one: Team A — 42" +
            "\n:two: Team B — 20" +
            "\n:three: Team C — 4" +
            "\n4. Team D — 1");
    }

    [Test]
    public void ComposeSeasonStandings_EmptyStandings_ShowsNoDataYetMessage()
    {
        // Act
        var message = _composer.ComposeSeasonStandings("2026/27", []);

        // Assert
        message.Should().Be(
            "Лига Обогревателей Скамейки (сезон 2026/27):" +
            "\nПока никто не греет скамейку — очки на лавке ещё не потеряны.");
    }
}
