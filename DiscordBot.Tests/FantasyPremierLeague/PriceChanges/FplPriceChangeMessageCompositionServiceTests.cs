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
        var changes = new FplPlayerPriceChange[]
        {
            new(1, "Salah", 100, 105),
            new(2, "Haaland", 105, 100)
        };

        // Act
        var message = composer.Compose(changes);

        // Assert
        message.Should().Be(
            "💰 Изменения цен игроков FPL:\n" +
            "📈 Salah подорожал: 10,0 млн £ → 10,5 млн £ (+0,5 млн £)\n" +
            "📉 Haaland подешевел: 10,5 млн £ → 10,0 млн £ (-0,5 млн £)");
    }

    [Test]
    public void Compose_ExternalPlayerName_SanitizesDiscordMentions()
    {
        // Arrange
        var composer = new FplPriceChangeMessageCompositionService();

        // Act
        var message = composer.Compose(
            [new FplPlayerPriceChange(1, "@everyone Salah", 100, 101)]);

        // Assert
        message.Should().Contain("@\u200Beveryone Salah");
        message.Should().NotContain("@everyone Salah");
    }
}
