using AwesomeAssertions;
using DiscordBot.Commands;
using DiscordBot.WelcomeMessages;
using NUnit.Framework;

namespace DiscordBot.Tests.WelcomeMessages;

[TestFixture]
public sealed class WelcomeMessageTemplateRotatorTests
{
    [Test]
    public void Next_SixConsecutiveCalls_UsesTemplatesInOrderThenRestarts()
    {
        // Arrange
        var rotator = new WelcomeMessageTemplateRotator(
            new BotHelpMessageCompositionService());

        // Act
        var messages = Enumerable.Range(0, 6)
            .Select(_ => rotator.Next("<@42>"))
            .ToArray();

        // Assert
        messages[0].Should().StartWith(
            "**<@42> вошёл на сервер.** Мбаппе — диктатор, у Винисиуса новый подбородок, **Магуайр — свят. Мир стабилен.**");
        messages[1].Should().StartWith(
            "Добро пожаловать, <@42>. И главное — не паникуй. После семи с половиной миллионов лет вычислений Глубокомысленный наконец объявил ответ на главный вопрос жизни, Вселенной и всего такого.\n\n**Гарри Магуайр.**");
        messages[2].Should().StartWith("**Добро пожаловать, <@42>.**");
        messages[3].Should().StartWith(
            "**Зафиксирован новый пользователь: <@42>.**");
        messages[4].Should().StartWith("**СРОЧНО:** <@42>");
        messages.Take(5).Should().OnlyHaveUniqueItems();
        messages[5].Should().Be(messages[0]);
        messages.Should().OnlyContain(message =>
            message.Contains("<@42>") && !message.Contains("{user}"));
        messages.Should().OnlyContain(message =>
            message.Contains("**С чего начать**") &&
            message.Contains("`/help`") &&
            message.Contains(BotHelpMessageCompositionService.FplClassicLeagueUrl) &&
            message.Contains(BotHelpMessageCompositionService.UclLeagueUrl));
        messages.Should().OnlyContain(message => message.Length <= 2_000);
    }
}
