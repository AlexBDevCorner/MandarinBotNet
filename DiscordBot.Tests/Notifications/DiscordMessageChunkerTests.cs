using AwesomeAssertions;
using Discord;
using DiscordBot.Notifications;
using NUnit.Framework;

namespace DiscordBot.Tests.Notifications;

[TestFixture]
public sealed class DiscordMessageChunkerTests
{
    [Test]
    public void Split_MaximumSizeStandings_PreservesEveryEntryWithinDiscordLimit()
    {
        // Arrange
        var entries = Enumerable.Range(1, 250)
            .Select(rank => $"{rank}. Team {rank:D3} {rank * 10}")
            .ToArray();
        var message = string.Join('\n', entries);

        // Act
        var chunks = DiscordMessageChunker.Split(
            message,
            DiscordConfig.MaxMessageSize);

        // Assert
        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().OnlyContain(chunk =>
            chunk.Length <= DiscordConfig.MaxMessageSize);

        var publishedEntries = chunks
            .SelectMany(chunk => chunk.Split('\n'))
            .ToArray();
        publishedEntries.Should().BeEquivalentTo(
            entries,
            options => options.WithStrictOrdering());
    }

    [Test]
    public void Split_SingleLineExceedsLimit_SplitsWithoutDroppingCharacters()
    {
        // Arrange
        var message = new string('x', DiscordConfig.MaxMessageSize + 17);

        // Act
        var chunks = DiscordMessageChunker.Split(
            message,
            DiscordConfig.MaxMessageSize);

        // Assert
        chunks.Should().HaveCount(2);
        string.Concat(chunks).Should().Be(message);
    }
}
