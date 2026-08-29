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
            "🔥 Лига Обогревателей Скамейки — итоги тура 5 (сезон 2026/27):" +
            "\n\n🪑 Больше всех очков оставили на скамейке в этом туре:" +
            "\n:one: Team A — 17" +
            "\n:two: Team B — 7" +
            "\n\n🏅 Главный обогреватель скамейки тура: Haaland — 15 очков, греющих лавку команды Team A!" +
            "\n\n📊 Общий зачёт сезона 2026/27:" +
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
            "🔥 Лига Обогревателей Скамейки — итоги тура 5 (сезон 2026/27):" +
            "\n\n📊 Общий зачёт сезона 2026/27:");
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
            "🔥 Лига Обогревателей Скамейки (сезон 2026/27):" +
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
            "🔥 Лига Обогревателей Скамейки (сезон 2026/27):" +
            "\n✨ Пока никто не греет скамейку — очки на лавке ещё не потеряны.");
    }

    [Test]
    public void ComposeSeasonOverview_IncludesTrackingLatestStandingsRecordsAndGapWarning()
    {
        // Arrange
        var overview = new BenchWarmingSeasonOverview(
            "2026/27",
            new BenchWarmingTrackingInfo(5, 8, 3),
            [
                new BenchWarmingEntryStanding(100, "Team A", 42),
                new BenchWarmingEntryStanding(200, "Team B", 31)
            ],
            8,
            [
                new BenchWarmingEntryStanding(300, "Team C", 11),
                new BenchWarmingEntryStanding(100, "Team A", 17)
            ],
            new BenchWarmingSeasonRecords(
                new BenchWarmingEntryRoundStanding(3, 100, "Team A", 22),
                7.4,
                new BenchWarmingStreakRecord(200, "Team C", 3, 5, 7),
                new BenchWarmingStreakRecord(300, "Team D", 2, 6, 7)));

        // Act
        var message = _composer.ComposeSeasonOverview(overview);

        // Assert
        message.Should().Be(
            "🔥 Лига Обогревателей Скамейки — сезон 2026/27" +
            "\n📍 Статистика отслеживается с GW5 · учтено 3 тура" +
            "\n⚠️ В истории есть пропущенные туры." +
            "\n\n🪑 Последний тур — GW8" +
            "\n:one: Team C — 11" +
            "\n:two: Team A — 17" +
            "\n\n📊 Общий зачёт" +
            "\n:one: Team A — 42" +
            "\n:two: Team B — 31" +
            "\n\n🏆 Рекорды сезона" +
            "\n💥 Худшая скамейка: Team A — 22 очка (GW3)" +
            "\n📈 В среднем: 7.4 очка на скамейке на менеджера за тур" +
            "\n🔥 Серия 8+: Team C — 3 тура подряд" +
            "\n🧼 Чистая скамейка: Team D — 2 тура подряд");
    }

    [Test]
    public void ComposeRoundStandings_IncludesStandingsAndTopBenchPlayer()
    {
        // Arrange
        var round = new BenchWarmingRoundSummary(
            "2026/27",
            4,
            [
                new BenchWarmingEntryStanding(100, "Team A", 18),
                new BenchWarmingEntryStanding(300, "Team C", 13),
                new BenchWarmingEntryStanding(400, "Team D", 9),
                new BenchWarmingEntryStanding(200, "Team B", 2)
            ],
            new BenchWarmingPlayerPoints(100, "Team A", 1, "Palmer", 12));

        // Act
        var message = _composer.ComposeRoundStandings(round);

        // Assert
        message.Should().Be(
            "🔥 Лига Обогревателей Скамейки — GW4" +
            "\n:one: Team A — 18" +
            "\n:two: Team C — 13" +
            "\n:three: Team D — 9" +
            "\n4. Team B — 2" +
            "\n\n🏅 Главный обогреватель тура:\n" +
            "Palmer — 12 очков на скамейке Team A");
    }

    [Test]
    public void ComposeRoundStandings_NoPositiveBenchPlayer_OmitsPlayerLine()
    {
        // Arrange
        var round = new BenchWarmingRoundSummary(
            "2026/27",
            4,
            [new BenchWarmingEntryStanding(100, "Team A", 0)],
            null);

        // Act
        var message = _composer.ComposeRoundStandings(round);

        // Assert
        message.Should().Be(
            "🔥 Лига Обогревателей Скамейки — GW4" +
            "\n:one: Team A — 0");
    }

    [Test]
    public void ComposeTeamProfile_IncludesStatisticsAndChronologicalHistory()
    {
        // Arrange
        var profile = new BenchWarmingTeamProfile(
            "2026/27",
            100,
            "Bobrov FC",
            2,
            47,
            9.4,
            18,
            4,
            3,
            2,
            [
                new BenchWarmingEntryRoundStanding(2, 100, "Bobrov FC", 6),
                new BenchWarmingEntryRoundStanding(3, 100, "Bobrov FC", 11),
                new BenchWarmingEntryRoundStanding(4, 100, "Bobrov FC", 18),
                new BenchWarmingEntryRoundStanding(5, 100, "Bobrov FC", 12),
                new BenchWarmingEntryRoundStanding(6, 100, "Bobrov FC", 0)
            ]);

        // Act
        var message = _composer.ComposeTeamProfile(profile);

        // Assert
        message.Should().Be(
            "🔥 Лига Обогревателей Скамейки — Bobrov FC — скамейка 2026/27" +
            "\n📍 Данные команды с GW2" +
            "\n\n💺 Всего оставлено: 47 очков" +
            "\n📊 Среднее: 9.4 очка/GW" +
            "\n💥 Рекорд: 18 очков — GW4" +
            "\n🔥 Серия 8+: 3 тура" +
            "\n🧼 Лучшая чистая серия: 2 тура" +
            "\n\nИстория:" +
            "\nGW2 — 6" +
            "\nGW3 — 11 🔥" +
            "\nGW4 — 18 💥" +
            "\nGW5 — 12 🔥" +
            "\nGW6 — 0 🧼");
    }

    [Test]
    public void ComposeTeamProfile_BenchBoostRound_IsMarkedWithBenchBoostNotCleanBench()
    {
        // Arrange
        var profile = new BenchWarmingTeamProfile(
            "2026/27",
            100,
            "Bobrov FC",
            5,
            0,
            0d,
            0,
            5,
            0,
            0,
            [new BenchWarmingEntryRoundStanding(5, 100, "Bobrov FC", 0, ActiveChip: "bboost")]);

        // Act
        var message = _composer.ComposeTeamProfile(profile);

        // Assert
        message.Should().Contain("GW5 — 0 🃏");
        message.Should().NotContain("GW5 — 0 🧼");
    }

    [Test]
    public void ComposeTeamNotFound_ReturnsFriendlyMessage()
    {
        // Act
        var message = _composer.ComposeTeamNotFound("ZZZ");

        // Assert
        message.Should().Be(
            "🔥 Команда \"ZZZ\" не найдена в Лиге обогревателей скамейки.");
    }

    [Test]
    public void ComposeAmbiguousTeam_ListsCandidatesWithIds()
    {
        // Act
        var message = _composer.ComposeAmbiguousTeam(
            "United",
            [
                new BenchWarmingTeamCandidate(12345, "Fraud United"),
                new BenchWarmingTeamCandidate(67890, "Maguire United")
            ]);

        // Assert
        message.Should().Be(
            "Не удалось однозначно найти команду \"United\"." +
            "\n\nВозможные варианты:" +
            "\n• Fraud United — ID 12345" +
            "\n• Maguire United — ID 67890");
    }

    [Test]
    public void ComposeRoundNotTracked_ExplainsTrackingStart()
    {
        // Act
        var message = _composer.ComposeRoundNotTracked(
            1,
            new BenchWarmingTrackingInfo(2, 5, 4));

        // Assert
        message.Should().Be(
            "🔥 Данных Лиги обогревателей за GW1 нет.\n" +
            "📍 Отслеживание началось с GW2.");
    }

    [Test]
    public void ComposeRoundStandings_SanitizesExternalPlayerAndTeamNames()
    {
        // Arrange
        var round = new BenchWarmingRoundSummary(
            "2026/27",
            4,
            [new BenchWarmingEntryStanding(100, "@everyone", 18)],
            new BenchWarmingPlayerPoints(100, "@everyone", 1, "@danger", 12));

        // Act
        var message = _composer.ComposeRoundStandings(round);

        // Assert
        message.Should().Contain("@\u200Bdanger");
        message.Should().NotContain("@danger");
        message.Should().Contain("@\u200Beveryone");
        message.Should().NotContain("@everyone");
    }
}
