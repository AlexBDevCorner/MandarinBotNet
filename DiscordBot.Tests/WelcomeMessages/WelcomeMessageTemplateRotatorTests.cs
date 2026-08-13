using AwesomeAssertions;
using DiscordBot.WelcomeMessages;
using NUnit.Framework;

namespace DiscordBot.Tests.WelcomeMessages;

[TestFixture]
public sealed class WelcomeMessageTemplateRotatorTests
{
    [Test]
    public void Next_FiveConsecutiveCalls_UsesTemplatesInOrderThenRestarts()
    {
        // Arrange
        var rotator = new WelcomeMessageTemplateRotator();

        // Act
        var messages = Enumerable.Range(0, 5)
            .Select(_ => rotator.Next("<@42>"))
            .ToArray();

        // Assert
        messages[0].Should().StartWith("**Добро пожаловать, <@42>.**");
        messages[1].Should().Be(
            "Добро пожаловать, <@42>. И главное — не паникуй. После семи с половиной миллионов лет вычислений Глубокомысленный наконец объявил ответ на главный вопрос жизни, Вселенной и всего такого.\n\n**Гарри Магуайр.**");
        messages[2].Should().StartWith(
            "**Зафиксирован новый пользователь: <@42>.**");
        messages[3].Should().StartWith("**СРОЧНО:** <@42>");
        messages.Take(4).Should().OnlyHaveUniqueItems();
        messages[4].Should().Be(messages[0]);
        messages.Should().OnlyContain(message =>
            message.Contains("<@42>") && !message.Contains("{user}"));
    }
}
