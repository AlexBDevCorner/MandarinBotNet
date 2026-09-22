using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.PriceChanges;
using DiscordBot.Notifications;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.PriceChanges;

[TestFixture]
public sealed class FplPriceChangeMessageCompositionServiceTests
{
    [Test]
    public void Compose_IncreasesAndDecreases_UsesRussianTextAndPriceFormatting()
    {
        // Arrange
        var composer = new FplPriceChangeMessageCompositionService();
        var report = new FplLeaguePriceChangeReport(
            DateTimeOffset.Parse("2026-08-30T10:00:00Z"),
            [
                new(
                    new FplPlayerPriceChange(1, "Salah", 100, 105),
                    ["Bobrov FC"]),
                new(
                    new FplPlayerPriceChange(2, "Haaland", 105, 100),
                    ["Bobrov FC", "Fraud United"])
            ],
            [
                new FplTeamPriceImpact(2, "Fraud United", -5)
            ],
            LeagueManagerCount: 2,
            AvailableSquadCount: 2,
            LeagueDataAvailable: true);

        // Act
        var message = composer.Compose(report);

        // Assert
        message.Should().Be(
            "💰 Изменения цен FPL с последней проверки:\n" +
            "💸 Общая стоимость составов: -0,5 млн £\n" +
            "📈 Salah: 10,0 млн £ → 10,5 млн £ (+0,5 млн £)\n" +
            "  В составах: Bobrov FC\n" +
            "📉 Haaland: 10,5 млн £ → 10,0 млн £ (-0,5 млн £)\n" +
            "  В составах: Bobrov FC, Fraud United\n\n" +
            "📋 По командам:\n" +
            "• Fraud United: -0,5 млн £");
    }

    [Test]
    public void Compose_ExternalPlayerName_SanitizesDiscordMentions()
    {
        // Arrange
        var composer = new FplPriceChangeMessageCompositionService();

        // Act
        var message = composer.Compose(new FplLeaguePriceChangeReport(
            DateTimeOffset.UtcNow,
            [new(
                new FplPlayerPriceChange(1, "@everyone Salah", 100, 101),
                ["@everyone FC"])],
            [new FplTeamPriceImpact(1, "@everyone FC", 1)],
            LeagueManagerCount: 1,
            AvailableSquadCount: 1,
            LeagueDataAvailable: true));

        // Assert
        message.Should().Contain("@\u200Beveryone Salah");
        message.Should().NotContain("@everyone Salah");
        message.Should().Contain("@\u200Beveryone FC");
    }

    [Test]
    public void Compose_OwnedAndUnownedChanges_ListsBothWithOwnershipOnlyForOwned()
    {
        // Arrange: Player A decreased and unowned, Player B owned.
        var composer = new FplPriceChangeMessageCompositionService();
        var report = new FplLeaguePriceChangeReport(
            DateTimeOffset.UtcNow,
            [
                new(new FplPlayerPriceChange(30, "Saka", 100, 99), []),
                new(new FplPlayerPriceChange(10, "Salah", 100, 101), ["Bobrov FC"])
            ],
            [new FplTeamPriceImpact(1, "Bobrov FC", 1)],
            LeagueManagerCount: 2,
            AvailableSquadCount: 2,
            LeagueDataAvailable: true);

        // Act
        var message = composer.Compose(report);

        // Assert: both players visible, only owned has annotation.
        message.Should().Contain("Saka");
        message.Should().Contain("Salah");
        message.Should().Contain("📉 Saka: 10,0 млн £ → 9,9 млн £ (-0,1 млн £)");
        message.Should().Contain("📈 Salah: 10,0 млн £ → 10,1 млн £ (+0,1 млн £)");
        message.Should().Contain("В составах: Bobrov FC");
        CountOccurrences(message, "В составах:").Should().Be(1);

        // Ordering preserved: Saka (first in report) appears before Salah.
        message.IndexOf("Saka", StringComparison.Ordinal)
            .Should().BeLessThan(message.IndexOf("Salah", StringComparison.Ordinal));

        // Aggregate reflects only owned-player impact, not the unowned market move.
        message.Should().Contain("💸 Общая стоимость составов: +0,1 млн £");
        message.Should().Contain("📋 По командам:");
        message.Should().Contain("• Bobrov FC: +0,1 млн £");
    }

    [Test]
    public void Compose_OnlyUnownedChanges_ListsAllWithLeagueNotAffected()
    {
        // Arrange
        var composer = new FplPriceChangeMessageCompositionService();
        var report = new FplLeaguePriceChangeReport(
            DateTimeOffset.UtcNow,
            [
                new(new FplPlayerPriceChange(1, "Salah", 100, 101), []),
                new(new FplPlayerPriceChange(30, "Saka", 80, 79), [])
            ],
            [],
            LeagueManagerCount: 2,
            AvailableSquadCount: 2,
            LeagueDataAvailable: true);

        // Act
        var message = composer.Compose(report);

        // Assert: all market changes listed plus league-not-affected note.
        message.Should().Contain("Salah");
        message.Should().Contain("Saka");
        message.Should().Contain("📈 Salah: 10,0 млн £ → 10,1 млн £ (+0,1 млн £)");
        message.Should().Contain("📉 Saka: 8,0 млн £ → 7,9 млн £ (-0,1 млн £)");
        message.Should().Contain("составы нашей лиги это не затронуло");
        message.Should().NotContain("Общая стоимость составов");
        message.Should().NotContain("По командам:");
        message.Should().NotContain("В составах:");
    }

    [Test]
    public void Compose_UnownedMarketMoves_DoNotAlterTeamImpacts()
    {
        // Arrange: large unowned rise must not leak into league totals.
        var composer = new FplPriceChangeMessageCompositionService();
        var report = new FplLeaguePriceChangeReport(
            DateTimeOffset.UtcNow,
            [
                new(new FplPlayerPriceChange(20, "Haaland", 100, 105), []),
                new(new FplPlayerPriceChange(10, "Salah", 100, 101), ["Bobrov FC"])
            ],
            [new FplTeamPriceImpact(1, "Bobrov FC", 1)],
            LeagueManagerCount: 2,
            AvailableSquadCount: 2,
            LeagueDataAvailable: true);

        // Act
        var message = composer.Compose(report);

        // Assert
        message.Should().Contain("Haaland");
        message.Should().Contain("Salah");
        message.Should().Contain("💸 Общая стоимость составов: +0,1 млн £");
        message.Should().NotContain("+0,6 млн £");
        message.Should().Contain("• Bobrov FC: +0,1 млн £");
        message.Should().NotContain("Haaland: 10,0 млн £ → 10,5 млн £ (+0,5 млн £)\n  В составах:");
    }

    [Test]
    public void Compose_LeagueDataUnavailable_ListsAllWithFallbackWarning()
    {
        // Arrange
        var composer = new FplPriceChangeMessageCompositionService();
        var report = new FplLeaguePriceChangeReport(
            DateTimeOffset.UtcNow,
            [
                new(new FplPlayerPriceChange(1, "Salah", 100, 101), []),
                new(new FplPlayerPriceChange(30, "Saka", 80, 79), [])
            ],
            [],
            LeagueManagerCount: 0,
            AvailableSquadCount: 0,
            LeagueDataAvailable: false);

        // Act
        var message = composer.Compose(report);

        // Assert
        message.Should().Contain("Salah");
        message.Should().Contain("Saka");
        message.Should().Contain("Не удалось сопоставить игроков с составами нашей лиги.");
    }

    [Test]
    public void Compose_PartialSquadData_ReportsCoverage()
    {
        // Arrange
        var composer = new FplPriceChangeMessageCompositionService();
        var report = new FplLeaguePriceChangeReport(
            DateTimeOffset.UtcNow,
            [new(new FplPlayerPriceChange(1, "Salah", 100, 101), ["Bobrov FC"])],
            [new FplTeamPriceImpact(1, "Bobrov FC", 1)],
            LeagueManagerCount: 3,
            AvailableSquadCount: 2,
            LeagueDataAvailable: true);

        // Act
        var message = composer.Compose(report, isLatestSavedBatch: true);

        // Assert
        message.Should().StartWith("💰 Последние зафиксированные");
        message.Should().Contain("Составы загружены для 2 из 3 менеджеров");
    }

    [Test]
    public void Compose_PartialSquadData_ListsAllChangesAndReportsCoverage()
    {
        // Arrange: partial squads still enrich loaded teams but show every change.
        var composer = new FplPriceChangeMessageCompositionService();
        var report = new FplLeaguePriceChangeReport(
            DateTimeOffset.UtcNow,
            [
                new(new FplPlayerPriceChange(30, "Saka", 100, 99), []),
                new(new FplPlayerPriceChange(1, "Salah", 100, 101), ["Bobrov FC"])
            ],
            [new FplTeamPriceImpact(1, "Bobrov FC", 1)],
            LeagueManagerCount: 3,
            AvailableSquadCount: 2,
            LeagueDataAvailable: true);

        // Act
        var message = composer.Compose(report);

        // Assert
        message.Should().Contain("Saka");
        message.Should().Contain("Salah");
        message.Should().Contain("В составах: Bobrov FC");
        message.Should().Contain("Составы загружены для 2 из 3 менеджеров");
        message.Should().Contain("💸 Общая стоимость составов: +0,1 млн £");
    }

    [Test]
    public void Compose_UnownedExternalNames_RemainDiscordSafe()
    {
        // Arrange
        var composer = new FplPriceChangeMessageCompositionService();
        var report = new FplLeaguePriceChangeReport(
            DateTimeOffset.UtcNow,
            [new(new FplPlayerPriceChange(30, "@everyone Saka", 100, 99), [])],
            [],
            LeagueManagerCount: 2,
            AvailableSquadCount: 2,
            LeagueDataAvailable: true);

        // Act
        var message = composer.Compose(report);

        // Assert
        message.Should().Contain("@\u200Beveryone Saka");
        message.Should().NotContain("@everyone Saka");
        message.Should().Contain("составы нашей лиги это не затронуло");
    }

    [Test]
    public void Compose_LargeBatch_PreservesAllThroughChunking()
    {
        // Arrange: synthetic batch longer than a single Discord message.
        var composer = new FplPriceChangeMessageCompositionService();
        var playerChanges = Enumerable.Range(1, 150)
            .Select(id => new FplLeaguePlayerPriceChange(
                new FplPlayerPriceChange(id, $"Player{id:D3}", 100, 101),
                id == 1 ? ["Bobrov FC"] : []))
            .ToArray();
        var report = new FplLeaguePriceChangeReport(
            DateTimeOffset.UtcNow,
            playerChanges,
            [new FplTeamPriceImpact(1, "Bobrov FC", 1)],
            LeagueManagerCount: 2,
            AvailableSquadCount: 2,
            LeagueDataAvailable: true);

        // Act
        var message = composer.Compose(report);
        var chunks = DiscordMessageChunker.Split(message, 2_000);

        // Assert
        message.Length.Should().BeGreaterThan(2_000);
        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().OnlyContain(chunk => chunk.Length <= 2_000);
        var combined = string.Join('\n', chunks);
        foreach (var playerChange in playerChanges)
        {
            combined.Should().Contain(playerChange.Change.PlayerName);
        }

        combined.Should().Contain("В составах: Bobrov FC");
    }

    private static int CountOccurrences(string text, string substring)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(substring, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += substring.Length;
        }

        return count;
    }
}
