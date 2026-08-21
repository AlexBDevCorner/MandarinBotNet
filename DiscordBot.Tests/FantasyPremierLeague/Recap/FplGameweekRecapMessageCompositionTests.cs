using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recap;
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
        var composer = new PremierLeagueMessageCompositionService(
            new ConfiguredTimeZone(new JobSchedulesOptions { TimeZoneId = "Europe/Riga" }));

        // Act
        var message = composer.ComposeGameweekRecap(recap);

        // Assert
        message.Should().Contain("итоги тура 5 (сезон 2026/27)");
        message.Should().Contain("Gameweek Winner: Alpha — 80 очков");
        message.Should().Contain("Highest score: Alpha — 80 очков");
        message.Should().Contain("Lowest score: Beta — 40 очков");
        message.Should().Contain("League average: 60.0");
        message.Should().Contain("Biggest climber: Alpha (+2)");
        message.Should().Contain("Biggest faller: (нет изменений)");
        message.Should().Contain("Alpha (+2)");
        message.Should().Contain("Fraud of the Week: Beta — 40 очков");
        message.Should().Contain("Benchmaster: Alpha — 8 очков");
        message.Should().Contain("Captain Genius: Alpha (Salah, 30 очков)");
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
}
