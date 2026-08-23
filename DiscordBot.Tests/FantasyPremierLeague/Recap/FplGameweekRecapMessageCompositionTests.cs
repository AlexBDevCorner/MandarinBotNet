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
    public void ComposeGameweekRecap_CalculatedMetrics_IncludesScoresMovementAndAwards()
    {
        // Arrange
        var snapshot = new FplGameweekSnapshot(
            "2026/27",
            5,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            [
                CreateManager(10, "Alpha", 80, 1, 3, 8, "Salah", 15),
                CreateManager(20, "Beta", 40, 2, 2, 0, "Son", 8)
            ]);
        var recap = new FplGameweekRecapCalculationService().Calculate(snapshot);
        recap = recap with
        {
            Achievements =
            [
                CreateAchievement("first-blood", "First Blood"),
                CreateAchievement("bench-warmer", "Bench Warmer"),
                CreateAchievement("captain-disaster", "Captain Disaster"),
                CreateAchievement("differential-merchant", "Differential Merchant"),
                CreateAchievement("minus-eight-enjoyer", "-8 Enjoyer")
            ],
            Ratings =
            [
                new FplManagerRating(
                    123,
                    "2026/27",
                    5,
                    10,
                    "Alpha",
                    "Manager Alpha",
                    1,
                    64,
                    42,
                    "v1",
                    DateTimeOffset.UtcNow)
            ]
        };
        var composer = new PremierLeagueMessageCompositionService(
            new ConfiguredTimeZone(new JobSchedulesOptions { TimeZoneId = "Europe/Riga" }));

        // Act
        var message = composer.ComposeGameweekRecap(recap);

        // Assert
        message.Should().Contain("итоги тура 5 (сезон 2026/27)");
        message.Should().Contain("👑 Победитель тура: Alpha — 80 очков");
        message.Should().Contain("🚀 Лучший результат: Alpha — 80 очков");
        message.Should().Contain("🫣 Худший результат: Beta — 40 очков");
        message.Should().Contain("📈 Средний балл лиги: 60.0");
        message.Should().Contain("🧗 Главный взлёт: Alpha (+2)");
        message.Should().Contain("🪂 Главное падение: (нет изменений)");
        message.Should().Contain("Alpha (+2)");
        message.Should().Contain("🤡 Фрод тура: Beta — 40 очков");
        message.Should().Contain("🪑 Повелитель скамейки: Alpha — 8 очков");
        message.Should().Contain("🧠 Капитанский гений: Alpha (Salah, 30 очков)");
        message.Should().Contain("🎖️ Достижения:");
        message.Should().Contain("Alpha — 🩸 Первая кровь");
        message.Should().Contain("Alpha — 🔥 Обогреватель скамейки");
        message.Should().Contain("Alpha — 💥 Капитанская катастрофа");
        message.Should().Contain("Alpha — 💎 Повелитель дифференциалов");
        message.Should().Contain("Alpha — 💸 Любитель минус восьми");
        message.Should().Contain("📊 Рейтинги:");
        message.Should().Contain(
            "Alpha — 🤡 Рейтинг фрода 64/100; 🗿 Индекс Магуайра 42/100");
    }

    [Test]
    public void ComposeGameweekRecap_UnsafeExternalNames_DoesNotAllowMentions()
    {
        // Arrange
        var manager = CreateManager(
            10,
            "@everyone\nInjected",
            80,
            1,
            1,
            0,
            "@here\nCaptain",
            15);
        var snapshot = new FplGameweekSnapshot(
            "2026/27",
            5,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            [manager]);
        var recap = new FplGameweekRecapCalculationService().Calculate(snapshot);
        var composer = new PremierLeagueMessageCompositionService(
            new ConfiguredTimeZone(new JobSchedulesOptions { TimeZoneId = "Europe/Riga" }));

        // Act
        var message = composer.ComposeGameweekRecap(recap);

        // Assert
        message.Should().NotContain("@everyone");
        message.Should().NotContain("@here");
        message.Should().Contain("@\u200Beveryone Injected");
        message.Should().Contain("@\u200Bhere Captain");
    }

    private static FplManagerGameweekStatistics CreateManager(
        int entryId,
        string entryName,
        int eventScore,
        int rank,
        int lastRank,
        int benchPoints,
        string captainName,
        int captainPoints)
    {
        return new FplManagerGameweekStatistics(
            entryId,
            entryName,
            $"Manager {entryName}",
            eventScore,
            100,
            rank,
            lastRank,
            lastRank - rank,
            benchPoints,
            [
                new FplLineupPick(
                    entryId,
                    captainName,
                    1,
                    2,
                    IsCaptain: true,
                    IsViceCaptain: false,
                    Points: captainPoints),
                new FplLineupPick(
                    entryId + 1_000,
                    "Bench player",
                    12,
                    0,
                    IsCaptain: false,
                    IsViceCaptain: false,
                    Points: benchPoints)
            ]);
    }

    private static FplAchievementAward CreateAchievement(
        string key,
        string englishName)
    {
        return new FplAchievementAward(
            123,
            "2026/27",
            5,
            10,
            "Alpha",
            key,
            englishName,
            "Historical English description.",
            IsRepeatable: true,
            "v1",
            DateTimeOffset.UtcNow);
    }
}
