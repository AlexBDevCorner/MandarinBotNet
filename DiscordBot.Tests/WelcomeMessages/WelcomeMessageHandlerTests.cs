using AwesomeAssertions;
using DiscordBot.WelcomeMessages;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DiscordBot.Tests.WelcomeMessages;

[TestFixture]
public sealed class WelcomeMessageHandlerTests
{
    private string _testDirectory = null!;
    private string _imagePath = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "MandarinBotNet.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
        _imagePath = Path.Combine(_testDirectory, "pc7n1.jpg");
        File.WriteAllBytes(_imagePath, [1, 2, 3]);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Test]
    public async Task HandleAsync_Disabled_DoesNotResolveDestination()
    {
        // Arrange
        var resolver = new FakeDestinationResolver(
            new WelcomeMessageDestination(true, new FakeWelcomeMessageChannel()));
        var handler = CreateHandler(
            new WelcomeMessageOptions
            {
                Enabled = false,
                GuildId = 10,
                ChannelId = 100
            },
            resolver);

        // Act
        await handler.HandleAsync(new DiscordGuildMember(10, 42, "<@42>"));

        // Assert
        resolver.ResolveCount.Should().Be(0);
    }

    [Test]
    public async Task HandleAsync_OtherGuild_DoesNotResolveDestination()
    {
        // Arrange
        var resolver = new FakeDestinationResolver(
            new WelcomeMessageDestination(true, new FakeWelcomeMessageChannel()));
        var handler = CreateHandler(CreateEnabledOptions(), resolver);

        // Act
        await handler.HandleAsync(new DiscordGuildMember(11, 42, "<@42>"));

        // Assert
        resolver.ResolveCount.Should().Be(0);
    }

    [Test]
    public async Task HandleAsync_FourMatchingJoins_SendsImageAndRotatesMessages()
    {
        // Arrange
        var channel = new FakeWelcomeMessageChannel();
        var resolver = new FakeDestinationResolver(
            new WelcomeMessageDestination(true, channel));
        var handler = CreateHandler(CreateEnabledOptions(), resolver);

        // Act
        for (ulong userId = 1; userId <= 4; userId++)
        {
            await handler.HandleAsync(new DiscordGuildMember(
                10,
                userId,
                $"<@{userId}>"));
        }

        // Assert
        channel.Messages.Should().HaveCount(4);
        channel.Messages.Should().OnlyContain(message =>
            message.ImagePath == _imagePath);
        channel.Messages.Take(3).Select(message => message.Content)
            .Should().OnlyHaveUniqueItems();
        RemoveMention(channel.Messages[3].Content, "<@4>").Should().Be(
            RemoveMention(channel.Messages[0].Content, "<@1>"));
        channel.Messages.Select((message, index) => (message, index))
            .Should().OnlyContain(item =>
                item.message.Content.Contains($"<@{item.index + 1}>"));
    }

    [Test]
    public async Task HandleAsync_MissingChannelConfiguration_LogsAndSkips()
    {
        // Arrange
        var resolver = new FakeDestinationResolver(
            new WelcomeMessageDestination(true, new FakeWelcomeMessageChannel()));
        var logger = new RecordingLogger<WelcomeMessageHandler>();
        var handler = CreateHandler(
            new WelcomeMessageOptions
            {
                Enabled = true,
                GuildId = 10,
                ChannelId = 0
            },
            resolver,
            logger);

        // Act
        await handler.HandleAsync(new DiscordGuildMember(10, 42, "<@42>"));

        // Assert
        resolver.ResolveCount.Should().Be(0);
        logger.Entries.Should().ContainSingle()
            .Which.Properties["Outcome"].Should()
            .Be("SkippedInvalidConfiguration");
    }

    [TestCase(false, false, "SkippedGuildUnavailable")]
    [TestCase(true, false, "SkippedChannelUnavailable")]
    public async Task HandleAsync_UnavailableDestination_LogsAndSkips(
        bool guildAvailable,
        bool channelAvailable,
        string expectedOutcome)
    {
        // Arrange
        var channel = channelAvailable ? new FakeWelcomeMessageChannel() : null;
        var resolver = new FakeDestinationResolver(
            new WelcomeMessageDestination(guildAvailable, channel));
        var logger = new RecordingLogger<WelcomeMessageHandler>();
        var handler = CreateHandler(CreateEnabledOptions(), resolver, logger);

        // Act
        await handler.HandleAsync(new DiscordGuildMember(10, 42, "<@42>"));

        // Assert
        logger.Entries.Should().ContainSingle()
            .Which.Properties["Outcome"].Should().Be(expectedOutcome);
    }

    [Test]
    public async Task HandleAsync_DiscordRejectsUpload_LogsWithoutThrowing()
    {
        // Arrange
        var channel = new FakeWelcomeMessageChannel(
            new InvalidOperationException("Discord rejected the upload."));
        var resolver = new FakeDestinationResolver(
            new WelcomeMessageDestination(true, channel));
        var logger = new RecordingLogger<WelcomeMessageHandler>();
        var handler = CreateHandler(CreateEnabledOptions(), resolver, logger);

        // Act
        Func<Task> act = () => handler.HandleAsync(
            new DiscordGuildMember(10, 42, "<@42>"));

        // Assert
        await act.Should().NotThrowAsync();
        logger.Entries.Should().ContainSingle()
            .Which.Properties["Outcome"].Should().Be("DeliveryFailed");
    }

    private WelcomeMessageHandler CreateHandler(
        WelcomeMessageOptions options,
        IWelcomeMessageDestinationResolver resolver,
        ILogger<WelcomeMessageHandler>? logger = null)
    {
        return new WelcomeMessageHandler(
            options,
            new WelcomeMessageTemplateRotator(),
            resolver,
            new WelcomeMessageAsset(_imagePath),
            logger ?? new RecordingLogger<WelcomeMessageHandler>());
    }

    private static WelcomeMessageOptions CreateEnabledOptions()
    {
        return new WelcomeMessageOptions
        {
            Enabled = true,
            GuildId = 10,
            ChannelId = 100
        };
    }

    private static string RemoveMention(string message, string mention)
    {
        return message.Replace(mention, "{user}", StringComparison.Ordinal);
    }

    private sealed class FakeDestinationResolver(
        WelcomeMessageDestination destination)
        : IWelcomeMessageDestinationResolver
    {
        public int ResolveCount { get; private set; }

        public WelcomeMessageDestination Resolve(
            ulong guildId,
            ulong channelId)
        {
            ResolveCount++;
            return destination;
        }
    }

    private sealed class FakeWelcomeMessageChannel(Exception? exception = null)
        : IWelcomeMessageChannel
    {
        public List<SentWelcomeMessage> Messages { get; } = [];

        public Task SendAsync(string imagePath, string content)
        {
            if (exception is not null)
            {
                throw exception;
            }

            Messages.Add(new SentWelcomeMessage(imagePath, content));
            return Task.CompletedTask;
        }
    }

    private sealed record SentWelcomeMessage(
        string ImagePath,
        string Content);
}
