using AwesomeAssertions;
using DiscordBot.WelcomeMessages;
using NUnit.Framework;

namespace DiscordBot.Tests.WelcomeMessages;

[TestFixture]
public sealed class WelcomeMessageTemplateRotatorTests
{
    [Test]
    public void Next_FourConsecutiveCalls_UsesEveryTemplateThenRestarts()
    {
        // Arrange
        var rotator = new WelcomeMessageTemplateRotator();

        // Act
        var messages = Enumerable.Range(0, 4)
            .Select(_ => rotator.Next("<@42>"))
            .ToArray();

        // Assert
        messages.Take(3).Should().OnlyHaveUniqueItems();
        messages[3].Should().Be(messages[0]);
        messages.Should().OnlyContain(message =>
            message.Contains("<@42>") && !message.Contains("{user}"));
    }
}
