using AwesomeAssertions;
using DiscordBot.Commands;
using DiscordBot.FantasyPremierLeague.Chips;
using NUnit.Framework;

namespace DiscordBot.Tests.Commands;

[TestFixture]
public sealed class ChipsCommandHandlerTests
{
    [Test]
    public async Task HandleAsync_DefersBeforeSendingResponse()
    {
        var handler = CreateHandler();
        var interaction = new TestInteraction();

        await handler.HandleAsync(interaction);

        interaction.Operations.Should().StartWith("Defer");
    }

    [Test]
    public async Task HandleAsync_ReturnsChipGuide()
    {
        var handler = CreateHandler();
        var interaction = new TestInteraction();

        await handler.HandleAsync(interaction);

        interaction.Messages.Should().Contain(message =>
            message.Contains("Wildcard") && message.Contains("Triple Captain"));
    }

    [Test]
    public async Task HandleAsync_LongGuide_SplitsIntoDiscordSizedMessages()
    {
        var handler = CreateHandler();
        var interaction = new TestInteraction();

        await handler.HandleAsync(interaction);

        interaction.Messages.Should().OnlyContain(message => message.Length <= 2_000);
        interaction.Operations.Should().Contain("Followup");
    }

    [Test]
    public async Task HandleAsync_NullInteraction_Throws()
    {
        var handler = CreateHandler();

        await handler.Invoking(h => h.HandleAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    private static ChipsCommandHandler CreateHandler() =>
        new(new FplChipCatalog(), new FplChipMessageCompositionService());

    private sealed class TestInteraction : IDiscordSlashCommandInteraction
    {
        public string Name => DiscordApplicationCommands.ChipsName;

        public string UserMention => "<@1>";

        public List<string> Messages { get; } = [];

        public List<string> Operations { get; } = [];

        public string? GetStringOption(string name) => null;

        public long? GetIntegerOption(string name) => null;

        public Task RespondAsync(string content)
        {
            Operations.Add("Respond");
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task DeferAsync()
        {
            Operations.Add("Defer");
            return Task.CompletedTask;
        }

        public Task ModifyOriginalResponseAsync(string content)
        {
            Operations.Add("Modify");
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task FollowupAsync(string content)
        {
            Operations.Add("Followup");
            Messages.Add(content);
            return Task.CompletedTask;
        }
    }
}
