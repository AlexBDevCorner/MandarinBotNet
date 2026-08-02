using AwesomeAssertions;
using Discord;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DiscordBot.Tests;

[TestFixture]
public sealed class DiscordNetLogHandlerTests
{
    [TestCase(LogSeverity.Critical, LogLevel.Critical)]
    [TestCase(LogSeverity.Error, LogLevel.Error)]
    [TestCase(LogSeverity.Warning, LogLevel.Warning)]
    [TestCase(LogSeverity.Info, LogLevel.Information)]
    [TestCase(LogSeverity.Verbose, LogLevel.Trace)]
    [TestCase(LogSeverity.Debug, LogLevel.Debug)]
    public void ToLogLevel_DiscordSeverity_MapsToMicrosoftLevel(
        LogSeverity discordSeverity,
        LogLevel expectedLevel)
    {
        // Arrange & Act
        var level = DiscordNetLogHandler.ToLogLevel(discordSeverity);

        // Assert
        level.Should().Be(expectedLevel);
    }

    [Test]
    public async Task HandleAsync_ErrorMessage_PreservesExceptionAndStructuredProperties()
    {
        // Arrange
        var exception = new InvalidOperationException("Gateway failed.");
        var logger = new RecordingLogger<DiscordNetLogHandler>();
        var handler = new DiscordNetLogHandler(logger);
        var message = new LogMessage(
            LogSeverity.Error,
            "Gateway",
            "Connection closed",
            exception);

        // Act
        await handler.HandleAsync(message);

        // Assert
        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(exception);
        entry.Properties["Event"].Should().Be("Gateway");
        entry.Properties["DiscordMessage"].Should().Be("Connection closed");
    }
}
