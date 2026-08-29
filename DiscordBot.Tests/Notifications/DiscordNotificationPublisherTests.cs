using AwesomeAssertions;
using Discord;
using DiscordBot.Notifications;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DiscordBot.Tests.Notifications;

[TestFixture]
public sealed class DiscordNotificationPublisherTests
{
    private string _testDirectory = null!;
    private string _databasePath = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "MandarinBotNet.Tests",
            Guid.NewGuid().ToString("N"));
        _databasePath = Path.Combine(_testDirectory, "notification-state.db");
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Test]
    public async Task PublishOnceAsync_GuildUnavailable_SkipsDeliveryWithOutcome()
    {
        // Arrange
        var logger = new RecordingLogger<DiscordNotificationPublisher>();
        var resolver = new FakeChannelResolver(
            new DiscordNotificationDestination(false, null));
        var publisher = CreatePublisher(resolver, logger);

        // Act
        var result = await publisher.PublishOnceAsync(
            CreateTarget(),
            "event-42",
            NotificationTypes.Deadline24Hours,
            "Reminder",
            CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        resolver.ResolveCount.Should().Be(1);
        var warning = logger.Entries.Should().ContainSingle().Subject;
        warning.Level.Should().Be(LogLevel.Warning);
        warning.Properties["Outcome"].Should().Be("SkippedGuildUnavailable");
    }

    [Test]
    public async Task PublishOnceAsync_ChannelUnavailable_SkipsDeliveryWithOutcome()
    {
        // Arrange
        var logger = new RecordingLogger<DiscordNotificationPublisher>();
        var resolver = new FakeChannelResolver(
            new DiscordNotificationDestination(true, null));
        var publisher = CreatePublisher(resolver, logger);

        // Act
        var result = await publisher.PublishOnceAsync(
            CreateTarget(),
            "event-42",
            NotificationTypes.Deadline24Hours,
            "Reminder",
            CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        var warning = logger.Entries.Should().ContainSingle().Subject;
        warning.Level.Should().Be(LogLevel.Warning);
        warning.Properties["Outcome"].Should().Be("SkippedChannelUnavailable");
    }

    [Test]
    public async Task PublishOnceAsync_MultipartDeadlineMessage_MentionsEveryoneOnlyInFirstPart()
    {
        // Arrange
        var channel = new FakeNotificationChannel();
        var resolver = new FakeChannelResolver(
            new DiscordNotificationDestination(true, channel));
        var publisher = CreatePublisher(resolver);
        var message = new string('a', DiscordConfig.MaxMessageSize) + "tail";

        // Act
        var result = await publisher.PublishOnceAsync(
            CreateTarget(mentionEveryone: true),
            "event-42",
            NotificationTypes.Deadline24Hours,
            message,
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        channel.SentMessages.Should().HaveCount(2);
        channel.SentMessages[0].Content.Should().StartWith("@everyone ");
        channel.SentMessages[0].AllowEveryoneMention.Should().BeTrue();
        var reconstructedMessage =
            channel.SentMessages[0].Content["@everyone ".Length..] +
            channel.SentMessages[1].Content;
        reconstructedMessage.Should().Be(message);
        channel.SentMessages[1].AllowEveryoneMention.Should().BeFalse();
        channel.SentMessages.Should().OnlyContain(message =>
            message.Content.Length <= DiscordConfig.MaxMessageSize);
    }

    [Test]
    public async Task PublishOnceAsync_NotificationTypeNotAllowedToMentionEveryone_DoesNotMentionEveryone()
    {
        // Arrange
        var channel = new FakeNotificationChannel();
        var resolver = new FakeChannelResolver(
            new DiscordNotificationDestination(true, channel));
        var publisher = CreatePublisher(resolver);
        var message = "Standings update";

        // Act
        var result = await publisher.PublishOnceAsync(
            CreateTarget(mentionEveryone: true),
            "event-42",
            NotificationTypes.ClassicStandings,
            message,
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        channel.SentMessages.Should().ContainSingle();
        channel.SentMessages[0].Content.Should().Be(message);
        channel.SentMessages[0].AllowEveryoneMention.Should().BeFalse();
        channel.SentMessages[0].Content.Should().NotStartWith("@everyone ");
    }

    [Test]
    public async Task PublishOnceAsync_SecondPartFailsAfterRestart_RetriesOnlyMissingPart()
    {
        // Arrange
        var message = new string('a', DiscordConfig.MaxMessageSize) + "tail";
        var firstChannel = new FakeNotificationChannel(failOnAttempt: 2);
        var firstPublisher = CreatePublisher(new FakeChannelResolver(
            new DiscordNotificationDestination(true, firstChannel)));

        // Act
        Func<Task> firstDelivery = () => firstPublisher.PublishOnceAsync(
            CreateTarget(),
            "event-42",
            NotificationTypes.ClassicStandings,
            message,
            CancellationToken.None);
        await firstDelivery.Should().ThrowAsync<InvalidOperationException>();

        var restartedChannel = new FakeNotificationChannel();
        var restartedPublisher = CreatePublisher(new FakeChannelResolver(
            new DiscordNotificationDestination(true, restartedChannel)));
        var retryResult = await restartedPublisher.PublishOnceAsync(
            CreateTarget(),
            "event-42",
            NotificationTypes.ClassicStandings,
            message,
            CancellationToken.None);

        // Assert
        retryResult.Should().BeTrue();
        firstChannel.SentMessages.Should().ContainSingle();
        restartedChannel.SentMessages.Should().ContainSingle();
        restartedChannel.SentMessages[0].Content.Should().Be("tail");
    }

    private DiscordNotificationPublisher CreatePublisher(
        IDiscordNotificationChannelResolver resolver,
        ILogger<DiscordNotificationPublisher>? logger = null)
    {
        var coordinator = new NotificationDeliveryCoordinator(
            new SqliteNotificationCheckpointStore(_databasePath),
            TimeProvider.System,
            new RecordingLogger<NotificationDeliveryCoordinator>());
        return new DiscordNotificationPublisher(
            resolver,
            coordinator,
            logger ?? new RecordingLogger<DiscordNotificationPublisher>());
    }

    private static NotificationTargetOptions CreateTarget(
        bool mentionEveryone = false)
    {
        return new NotificationTargetOptions
        {
            GuildId = 10,
            ChannelId = 100,
            MentionEveryone = mentionEveryone
        };
    }

    private sealed class FakeChannelResolver(
        DiscordNotificationDestination destination)
        : IDiscordNotificationChannelResolver
    {
        public int ResolveCount { get; private set; }

        public DiscordNotificationDestination Resolve(
            ulong guildId,
            ulong channelId)
        {
            ResolveCount++;
            return destination;
        }
    }

    private sealed class FakeNotificationChannel(int? failOnAttempt = null)
        : IDiscordNotificationChannel
    {
        private int _attemptCount;

        public List<SentMessage> SentMessages { get; } = [];

        public Task SendMessageAsync(string content, bool allowEveryoneMention)
        {
            _attemptCount++;
            if (_attemptCount == failOnAttempt)
            {
                throw new InvalidOperationException("Discord rejected the message.");
            }

            SentMessages.Add(new SentMessage(content, allowEveryoneMention));
            return Task.CompletedTask;
        }
    }

    private sealed record SentMessage(
        string Content,
        bool AllowEveryoneMention);
}
