using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.PriceChanges;
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
    public void Compose_NoLeagueSquadsAffected_HidesGlobalNoise()
    {
        // Arrange
        var composer = new FplPriceChangeMessageCompositionService();
        var report = new FplLeaguePriceChangeReport(
            DateTimeOffset.UtcNow,
            [new(new FplPlayerPriceChange(1, "Salah", 100, 101), [])],
            [],
            LeagueManagerCount: 2,
            AvailableSquadCount: 2,
            LeagueDataAvailable: true);

        // Act
        var message = composer.Compose(report);

        // Assert
        message.Should().Contain("составы нашей лиги это не затронуло");
        message.Should().NotContain("Salah");
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
}
