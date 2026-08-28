using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recap;
using DiscordBot.FantasyPremierLeague.Recognition;
using DiscordBot.PremierLeague;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Recap;

[TestFixture]
public sealed class FplGameweekRecapMessageCompositionTests
{
    [Test]
    public void ComposeGameweekRecap_Narrative_HighlightsSeasonStandingsAndAchievements()
    {
        var maguire = CreateManager(40, "Maguire's Disciples", 70, 1247, 1, 1);
        var bobrov = CreateManager(10, "Bobrov FC", 83, 1241, 2, 1);
        var fraud = CreateManager(30, "@everyone Fraud", 39, 1203, 3, 2, transferCost: 8);
        var pelmeni = CreateManager(20, "Pelmeni United", 75, 1219, 4, 8);
        var bench = CreateManager(50, "Bench Masters", 50, 1197, 5, 5, benchPoints: 21, captainPoints: 2, viceCaptainPoints: 13);

        var benchCaptain = bench.Lineup.Single(pick => pick.IsCaptain);
        var benchViceCaptain = bench.Lineup.Single(pick => pick.IsViceCaptain);

        var recap = new FplGameweekRecap(
            "2026/27",
            5,
            [
                new FplGameweekWinnerHighlight(83, [bobrov]),
                new FplRankMovementHighlight(FplRecapHighlightKind.BiggestClimb, pelmeni, 8, 4),
                new FplCaptainDisasterHighlight(bench, benchCaptain, benchViceCaptain),
                new FplTransferHitHighlight(fraud, 8),
                new FplBenchDisasterHighlight(bench),
                new FplRankMovementHighlight(FplRecapHighlightKind.BiggestFall, bobrov, 1, 2)
            ],
            [
                new FplFirstTimeAtTopTrend(maguire)
            ],
            [maguire, bobrov, fraud, pelmeni, bench],
            [
                new FplAchievementAward(
                    123, "2026/27", 5, 10, "Bobrov FC", "differential-merchant",
                    "Differential Merchant", "Desc", IsRepeatable: true, "v1",
                    DateTimeOffset.UtcNow),
                new FplAchievementAward(
                    123, "2026/27", 5, 30, "@everyone Fraud", "minus-eight-enjoyer",
                    "-8 Enjoyer", "Desc", IsRepeatable: true, "v1",
                    DateTimeOffset.UtcNow)
            ]);

        var composer = CreateComposer();
        var message = composer.ComposeGameweekRecap(recap);

        message.Should().Contain("📊 GW5 — что произошло");
        message.Should().Contain("👑 Bobrov FC выиграл тур — 83 очка");
        message.Should().Contain("🚀 Pelmeni United взлетел с 8-го на 4-е место");
        message.Should().Contain("💥 Bench Masters: капитан Cap Bench Masters — 2, VC VC Bench Masters — 13");
        message.Should().Contain("💸 @\u200Beveryone Fraud взял -8 за трансферы и закончил тур с 39 очками");
        message.Should().Contain("🪑 Bench Masters оставил 21 очко на скамейке");
        message.Should().Contain("📈 Сюжет сезона");
        message.Should().Contain("👑 Maguire's Disciples впервые вышел на первое место");
        message.Should().Contain("🏆 Таблица");
        message.Should().Contain("🥇 Maguire's Disciples — 1 247");
        message.Should().Contain("🥈 Bobrov FC — 1 241 ↓1 — 6 до лидера");
        message.Should().Contain("🥉 @\u200Beveryone Fraud — 1 203 ↓1 — 44 до лидера");
        message.Should().Contain("4. Pelmeni United — 1 219 ↑4 — 28 до лидера");
        message.Should().Contain("5. Bench Masters — 1 197 — 50 до лидера");
        message.Should().Contain("🎖️ Достижения");
        message.Should().Contain("Bobrov FC — 💎 Повелитель дифференциалов");
        message.Should().Contain("@\u200Beveryone Fraud — 💸 Любитель минус восьми");

        message.Should().NotContain("Лучший результат");
        message.Should().NotContain("👑 Bobrov FC выиграл тур — 83 очка — 83");
        message.Should().NotContain("(нет данных)");
        message.Should().NotContain("(нет изменений)");
        message.Should().NotContain("@everyone");
        message.Should().NotContain("Fraud Rating");
        message.Should().NotContain("Maguire Index");
    }

    [Test]
    public void ComposeGameweekRecap_NoTrendsOrAchievements_OmitsOptionalSections()
    {
        var bobrov = CreateManager(10, "Bobrov FC", 83, 1241, 1, 1);

        var recap = new FplGameweekRecap(
            "2026/27",
            5,
            [new FplGameweekWinnerHighlight(83, [bobrov])],
            [],
            [bobrov],
            []);

        var message = CreateComposer().ComposeGameweekRecap(recap);

        message.Should().Contain("📊 GW5 — что произошло");
        message.Should().Contain("👑 Bobrov FC выиграл тур — 83 очка");
        message.Should().NotContain("📈 Сюжет сезона");
        message.Should().NotContain("🎖️ Достижения");
    }

    [Test]
    public void ComposeGameweekRecap_TiedRanks_RendersSharedPositionsAndNoGapForJointLeaders()
    {
        var leaderA = CreateManager(10, "Leader A", 70, 1247, 1, 2);
        var leaderB = CreateManager(20, "Leader B", 70, 1247, 1, 1);
        var third = CreateManager(30, "Third", 60, 1219, 3, 5);
        var fourth = CreateManager(40, "Fourth", 60, 1203, 4, 4);
        var fifth = CreateManager(50, "Fifth", 60, 1197, 5, 5);

        var recap = new FplGameweekRecap(
            "2026/27",
            5,
            [new FplGameweekWinnerHighlight(70, [leaderA])],
            [],
            [leaderA, leaderB, third, fourth, fifth],
            []);

        var message = CreateComposer().ComposeGameweekRecap(recap);

        message.Should().Contain("🥇 Leader A — 1 247 ↑1");
        message.Should().Contain("🥇 Leader B — 1 247");
        message.Should().Contain("🥉 Third — 1 219 ↑2 — 28 до лидера");
        message.Should().Contain("4. Fourth — 1 203 — 44 до лидера");
        message.Should().Contain("5. Fifth — 1 197 — 50 до лидера");
        message.Should().NotContain("🥈");
        message.Should().NotContain("Leader B — 1 247 —");
    }

    [Test]
    public void ComposeGameweekRecap_Pluralization_UsesCorrectFormsForFive()
    {
        var manager = CreateManager(10, "Streaker", 70, 1247, 1, 4);

        var recap = new FplGameweekRecap(
            "2026/27",
            5,
            [new FplGameweekWinnerHighlight(70, [manager])],
            [
                new FplRecentWinsTrend(manager, 5, 5),
                new FplConsecutiveRankRisesTrend(manager, 5)
            ],
            [manager],
            []);

        var message = CreateComposer().ComposeGameweekRecap(recap);

        message.Should().Contain("— 5 побед за последние 5 туров");
        message.Should().Contain("5 туров подряд");
        message.Should().NotContain("5 победы");
        message.Should().NotContain("5 тура подряд");
    }

    [Test]
    public void ComposeGameweekRecap_UnsafeExternalNames_DoesNotAllowMentions()
    {
        var manager = CreateManager(10, "@everyone\nInjected", 80, 1000, 1, 1, captainPoints: 2, viceCaptainPoints: 13);

        var recap = new FplGameweekRecap(
            "2026/27",
            5,
            [
                new FplGameweekWinnerHighlight(80, [manager]),
                new FplCaptainDisasterHighlight(
                    manager,
                    manager.Lineup.Single(p => p.IsCaptain),
                    manager.Lineup.Single(p => p.IsViceCaptain))
            ],
            [],
            [manager],
            []);

        var message = CreateComposer().ComposeGameweekRecap(recap);

        message.Should().NotContain("@everyone");
        message.Should().Contain("@\u200Beveryone Injected");
    }

    private static PremierLeagueMessageCompositionService CreateComposer() =>
        new(new ConfiguredTimeZone(new JobSchedulesOptions { TimeZoneId = "Europe/Riga" }));

    private static FplManagerGameweekStatistics CreateManager(
        int entryId,
        string entryName,
        int eventScore,
        int totalScore,
        int rank,
        int lastRank,
        int benchPoints = 0,
        int captainPoints = 10,
        int viceCaptainPoints = 0,
        int transferCost = 0)
    {
        return new FplManagerGameweekStatistics(
            entryId,
            entryName,
            $"Manager {entryName}",
            eventScore,
            totalScore,
            rank,
            lastRank,
            lastRank - rank,
            benchPoints,
            [
                new FplLineupPick(
                    entryId, $"Cap {entryName}", 1, 2, IsCaptain: true, IsViceCaptain: false, Points: captainPoints),
                new FplLineupPick(
                    entryId + 1, $"VC {entryName}", 2, 1, IsCaptain: false, IsViceCaptain: true, Points: viceCaptainPoints),
                new FplLineupPick(
                    entryId + 2, $"Bench {entryName}", 12, 0, IsCaptain: false, IsViceCaptain: false, Points: benchPoints)
            ])
        {
            TransferCost = transferCost
        };
    }
}
