using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Recognition;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Recognition;

[TestFixture]
public sealed class FplRecognitionMessageCompositionServiceTests
{
    private static readonly FplManagerProfile SampleProfile = new(
        Season: "2026/27",
        TrackingStartedEventId: 1,
        EntryId: 10,
        EntryName: "Bobrov FC",
        ManagerName: "Aleksandrs Bobrovs",
        CurrentRank: 4,
        TotalScore: 612,
        GameweekWins: 2,
        BestGameweekScore: 91,
        BestGameweekEventId: 5,
        HighestBenchPoints: 24,
        HighestBenchPointsEventId: 3,
        AchievementCounts:
        [
            new FplAchievementCount(FplAchievementKeys.BenchWarmer, "bench", 4),
            new FplAchievementCount(FplAchievementKeys.CaptainDisaster, "cap", 2),
            new FplAchievementCount(FplAchievementKeys.FirstBlood, "fb", 1)
        ]);

    private readonly FplRecognitionMessageCompositionService _composer = new();

    [Test]
    public void ComposeProfile_HeadingContainsTeamAndManager()
    {
        var message = _composer.ComposeProfile(SampleProfile);

        message.Should().Contain("🏅 Bobrov FC");
        message.Should().Contain("Aleksandrs Bobrovs");
    }

    [Test]
    public void ComposeProfile_RendersRankAndPoints()
    {
        var message = _composer.ComposeProfile(SampleProfile);

        message.Should().Contain("🏆 Место: 4");
        message.Should().Contain("⭐ Очки: 612");
    }

    [Test]
    public void ComposeProfile_RendersGameweekWins()
    {
        var message = _composer.ComposeProfile(SampleProfile);

        message.Should().Contain("👑 Побед в турах: 2");
    }

    [Test]
    public void ComposeProfile_RendersEarnedAchievementsWithRussianNames()
    {
        var message = _composer.ComposeProfile(SampleProfile);

        message.Should().Contain("🩸 Первая кровь ×1");
        message.Should().Contain("🔥 Обогреватель скамейки ×4");
        message.Should().Contain("💥 Капитанская катастрофа ×2");
        message.Should().NotContain("Повелитель дифференциалов");
    }

    [Test]
    public void ComposeProfile_RetiredDifferentialMerchant_IsIgnored()
    {
        var profile = SampleProfile with
        {
            AchievementCounts =
            [
                new FplAchievementCount(FplAchievementKeys.BenchWarmer, "bench", 1),
                new FplAchievementCount(
                    RetiredFplAchievementKeys.DifferentialMerchant,
                    "Differential Merchant",
                    3)
            ]
        };

        var message = _composer.ComposeProfile(profile);

        message.Should().Contain("🎖️ Достижения: 1");
        message.Should().Contain("🔥 Обогреватель скамейки ×1");
        message.Should().NotContain("Повелитель дифференциалов");
    }

    [Test]
    public void ComposeProfile_RendersPersonalRecords()
    {
        var message = _composer.ComposeProfile(SampleProfile);

        message.Should().Contain("⚡ Лучший тур: 91 очко — GW5");
        message.Should().Contain("🪑 Максимум на скамейке: 24 очка — GW3");
    }

    [Test]
    public void ComposeProfile_RendersTrackingStart()
    {
        var message = _composer.ComposeProfile(SampleProfile);

        message.Should().Contain("📍 Достижения отслеживаются с GW1");
    }

    [Test]
    public void ComposeProfile_SanitizesExternalNames()
    {
        var profile = SampleProfile with { EntryName = "Bobrov\nFC" };

        var message = _composer.ComposeProfile(profile);

        message.Should().Contain("🏅 Bobrov FC");
    }

    [Test]
    public void ComposeProfile_DoesNotMentionFraudRatingOrMaguireIndex()
    {
        var message = _composer.ComposeProfile(SampleProfile);

        message.Should().NotContain("Fraud Rating");
        message.Should().NotContain("Maguire Index");
    }

    [Test]
    public void ComposeSeasonSummary_HandlesTiedTotalRanking()
    {
        var summary = new FplAchievementSeasonSummary(
            "2026/27",
            1,
            [
                new FplAchievementTotalRankingEntry(10, "Bobrov FC", "A", 4, 10),
                new FplAchievementTotalRankingEntry(20, "Rival FC", "B", 2, 10)
            ],
            [],
            0,
            []);

        var message = _composer.ComposeSeasonSummary(summary);

        message.Should().Contain("🥇 Bobrov FC — 10");
        message.Should().Contain("🥇 Rival FC — 10");
    }

    [Test]
    public void ComposeSeasonSummary_OmitsEmptyCategories()
    {
        var summary = new FplAchievementSeasonSummary(
            "2026/27",
            1,
            [],
            [],
            0,
            []);

        var message = _composer.ComposeSeasonSummary(summary);

        message.Should().NotContain("👑 Победы в турах");
        message.Should().NotContain("🔥 Обогреватель скамейки");
    }

    [Test]
    public void ComposeSeasonSummary_RendersGameweekWinLeaders()
    {
        var summary = new FplAchievementSeasonSummary(
            "2026/27",
            1,
            [],
            [],
            3,
            [
                new FplManagerReference(10, "Bobrov FC", "A"),
                new FplManagerReference(20, "Rival FC", "B")
            ]);

        var message = _composer.ComposeSeasonSummary(summary);

        message.Should().Contain("👑 Победы в турах");
        message.Should().Contain("Bobrov FC — 3");
        message.Should().Contain("Rival FC — 3");
    }

    [Test]
    public void ComposeSeasonSummary_DoesNotMentionFraudRatingOrMaguireIndex()
    {
        var summary = new FplAchievementSeasonSummary(
            "2026/27",
            1,
            [],
            [
                new FplAchievementCategoryRanking(
                    FplAchievementKeys.BenchWarmer,
                    4,
                    [new FplManagerReference(10, "Bobrov FC", "A")])
            ],
            4,
            [new FplManagerReference(10, "Bobrov FC", "A")]);

        var message = _composer.ComposeSeasonSummary(summary);

        message.Should().NotContain("Fraud Rating");
        message.Should().NotContain("Maguire Index");
    }

    [Test]
    public void ComposeSeasonSummary_RetiredDifferentialMerchant_IsIgnored()
    {
        var summary = new FplAchievementSeasonSummary(
            "2026/27",
            1,
            [],
            [
                new FplAchievementCategoryRanking(
                    RetiredFplAchievementKeys.DifferentialMerchant,
                    3,
                    [new FplManagerReference(10, "Bobrov FC", "A")]),
                new FplAchievementCategoryRanking(
                    FplAchievementKeys.BenchWarmer,
                    2,
                    [new FplManagerReference(10, "Bobrov FC", "A")])
            ],
            0,
            []);

        var message = _composer.ComposeSeasonSummary(summary);

        message.Should().Contain("🔥 Обогреватель скамейки");
        message.Should().NotContain("Повелитель дифференциалов");
    }

    [Test]
    public void ComposeManagerNotFound_IncludesQuery()
    {
        _composer.ComposeManagerNotFound("Bob")
            .Should().Be("🤷 Не удалось найти менеджера \"Bob\".");
    }

    [Test]
    public void ComposeManagerNotFound_SanitizesQuery()
    {
        var message = _composer.ComposeManagerNotFound("@everyone");

        message.Should().NotContain("@everyone");
        message.Should().Contain("@\u200Beveryone");
    }

    [Test]
    public void ComposeAmbiguousManager_ListsCandidates()
    {
        var message = _composer.ComposeAmbiguousManager("bob",
        [
            new FplManagerReference(10, "Bobrov FC", "Aleksandrs Bobrovs"),
            new FplManagerReference(20, "Bob United", "Bob Smith")
        ]);

        message.Should().Contain("🤔 Нашлось несколько подходящих менеджеров:");
        message.Should().Contain("Bobrov FC — Aleksandrs Bobrovs");
        message.Should().Contain("Bob United — Bob Smith");
    }

    [Test]
    public void ComposeNoData_ReturnsFriendlyMessage()
    {
        _composer.ComposeNoData()
            .Should().Be("📭 Данные о достижениях пока недоступны. Попробуйте ещё раз позже.");
    }
}
