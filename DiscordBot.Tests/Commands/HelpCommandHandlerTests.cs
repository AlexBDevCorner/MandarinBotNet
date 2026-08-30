using AwesomeAssertions;
using DiscordBot.Commands;
using NUnit.Framework;

namespace DiscordBot.Tests.Commands;

[TestFixture]
public sealed class HelpCommandHandlerTests
{
    [Test]
    public async Task HandleAsync_ReturnsDiscoverableCommandsAndLeagueLinks()
    {
        // Arrange
        var handler = new HelpCommandHandler(
            new BotHelpMessageCompositionService());
        var interaction = new TestInteraction();

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Message.Should().Contain("`/live`");
        interaction.Message.Should().Contain("`/prices`");
        interaction.Message.Should().Contain("`/deadline`");
        interaction.Message.Should().Contain(
            BotHelpMessageCompositionService.FplClassicLeagueUrl);
        interaction.Message.Should().Contain(
            BotHelpMessageCompositionService.UclLeagueUrl);
        interaction.Message.Length.Should().BeLessThanOrEqualTo(2_000);
    }

    private sealed class TestInteraction : IDiscordSlashCommandInteraction
    {
        public string Name => DiscordApplicationCommands.HelpName;

        public string UserMention => "<@1>";

        public string Message { get; private set; } = string.Empty;

        public string? GetStringOption(string name) => null;

        public long? GetIntegerOption(string name) => null;

        public Task RespondAsync(string content)
        {
            Message = content;
            return Task.CompletedTask;
        }

        public Task DeferAsync() => Task.CompletedTask;

        public Task ModifyOriginalResponseAsync(string content) => Task.CompletedTask;

        public Task FollowupAsync(string content) => Task.CompletedTask;
    }
}
