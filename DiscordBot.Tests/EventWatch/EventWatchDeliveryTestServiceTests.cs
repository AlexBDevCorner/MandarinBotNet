using AwesomeAssertions;
using DiscordBot.EventWatch;
using DiscordBot.Notifications;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace DiscordBot.Tests.EventWatch;

[TestFixture]
public sealed class EventWatchDeliveryTestServiceTests
{
    private const string AtalantaWatchId = "riga-fc-atalanta-2026";

    [Test]
    public async Task SendTestAsync_SendsToConfiguredTargetWithoutEveryoneMention()
    {
        var watch = new EventWatchDefinition
        {
            Enabled = true,
            Id = AtalantaWatchId,
            Title = "Riga FC vs Atalanta tickets",
            MatchTerms = ["Atalanta"],
            Targets =
            [
                new NotificationTargetOptions
                {
                    GuildId = 10,
                    ChannelId = 100,
                    MentionEveryone = true
                }
            ]
        };
        var channel = new FakeChannel();
        var service = CreateService([watch], new FakeResolver(channel));
        var checkpointStore = new InMemoryCheckpointStore();

        var report = await service.SendTestAsync(CancellationToken.None);

        report.HasConfiguredTargets.Should().BeTrue();
        var target = report.Targets.Should().ContainSingle().Subject;
        target.Delivered.Should().BeTrue();
        target.GuildId.Should().Be(10);
        target.ChannelId.Should().Be(100);

        var sent = channel.Sent.Should().ContainSingle().Subject;
        sent.AllowMention.Should().BeFalse();
        sent.Content.Should().Contain("delivery test");
        sent.Content.Should().Contain(AtalantaWatchId);
        sent.Content.Should().Contain("guild 10");
        sent.Content.Should().Contain("channel 100");
        sent.Content.Should().NotContain("@everyone");

        // The diagnostic path bypasses checkpoints entirely.
        checkpointStore.IsDelivered(
            new NotificationCheckpoint(10, 100, $"event-watch:{AtalantaWatchId}", NotificationTypes.EventWatchAnnouncement))
            .Should().BeFalse();
        checkpointStore.IsDelivered(
            new NotificationCheckpoint(10, 100, $"event-watch:{AtalantaWatchId}", NotificationTypes.EventWatchTicketLink))
            .Should().BeFalse();
    }

    [Test]
    public async Task SendTestAsync_IsRepeatable()
    {
        var watch = new EventWatchDefinition
        {
            Enabled = true,
            Id = AtalantaWatchId,
            Title = "Riga FC vs Atalanta tickets",
            MatchTerms = ["Atalanta"],
            Targets = [new NotificationTargetOptions { GuildId = 10, ChannelId = 100 }]
        };
        var channel = new FakeChannel();
        var service = CreateService([watch], new FakeResolver(channel));

        var first = await service.SendTestAsync(CancellationToken.None);
        var second = await service.SendTestAsync(CancellationToken.None);

        first.Targets.Should().ContainSingle().Which.Delivered.Should().BeTrue();
        second.Targets.Should().ContainSingle().Which.Delivered.Should().BeTrue();
        channel.Sent.Should().HaveCount(2);
        channel.Sent.Should().OnlyContain(sent => !sent.AllowMention);
        channel.Sent.Should().OnlyContain(sent => !sent.Content.Contains("@everyone"));
    }

    [Test]
    public async Task SendTestAsync_NoEnabledTargets_ReportsMissingConfiguration()
    {
        var service = CreateService([], new FakeResolver(new FakeChannel()));

        var report = await service.SendTestAsync(CancellationToken.None);

        report.HasConfiguredTargets.Should().BeFalse();
        report.Targets.Should().BeEmpty();
    }

    [Test]
    public void ComposeTestMessage_NeverContainsEveryoneMention()
    {
        var watch = new EventWatchDefinition
        {
            Enabled = true,
            Id = AtalantaWatchId,
            Title = "Riga FC vs Atalanta tickets",
            MatchTerms = ["Atalanta"],
            Targets = [new NotificationTargetOptions { GuildId = 10, ChannelId = 100, MentionEveryone = true }]
        };

        var message = EventWatchDeliveryTestService.ComposeTestMessage(
            watch,
            watch.Targets[0]);

        message.Should().Contain("delivery test");
        message.Should().Contain(AtalantaWatchId);
        message.Should().NotContain("@everyone");
    }

    private static EventWatchDeliveryTestService CreateService(
        IReadOnlyList<EventWatchDefinition> watches,
        IDiscordNotificationChannelResolver resolver)
    {
        var botOptions = new MandarinBotOptions
        {
            EventWatch = new EventWatchOptions { Watches = watches.ToList() }
        };
        return new EventWatchDeliveryTestService(
            Options.Create(botOptions),
            resolver,
            new RecordingLogger<EventWatchDeliveryTestService>());
    }

    private sealed class InMemoryCheckpointStore : INotificationCheckpointStore
    {
        private readonly HashSet<NotificationCheckpoint> _delivered = [];

        public bool IsDelivered(NotificationCheckpoint checkpoint)
        {
            lock (_delivered)
            {
                return _delivered.Contains(checkpoint);
            }
        }

        public void RecordDelivered(NotificationCheckpoint checkpoint, DateTimeOffset deliveredAtUtc)
        {
            lock (_delivered)
            {
                _delivered.Add(checkpoint);
            }
        }
    }

    private sealed class FakeChannel : IDiscordNotificationChannel
    {
        public List<(string Content, bool AllowMention)> Sent { get; } = [];

        public Task SendMessageAsync(string content, bool allowEveryoneMention)
        {
            Sent.Add((content, allowEveryoneMention));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeResolver(FakeChannel channel) : IDiscordNotificationChannelResolver
    {
        public DiscordNotificationDestination Resolve(ulong guildId, ulong channelId) =>
            new(true, channel);
    }
}
