using AwesomeAssertions;
using DiscordBot.Commands;
using DiscordBot.EventWatch;
using DiscordBot.EventWatch.Commands;
using DiscordBot.Notifications;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace DiscordBot.Tests.EventWatch;

[TestFixture]
public sealed class EventWatchCommandHandlerTests
{
    private const string AtalantaWatchId = "riga-fc-atalanta-2026";

    [Test]
    public async Task HandleAsync_StatusSubcommand_RespondsWithoutPublishing()
    {
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Fixture on Saturday at Skonto Stadium.</p></article></div>
            </body></html>
            """;
        var observations = new RigaFcPageParser().Parse(html, new Uri("https://rigafc.lv/"));
        var botOptions = CreateOptions(schedulingEnabled: true);
        var statusService = new EventWatchStatusService(
            new FixedSource(observations),
            new EventWatchSignalDetector(),
            Options.Create(botOptions),
            new RecordingLogger<EventWatchStatusService>());
        var channel = new FakeChannel();
        var testService = new EventWatchDeliveryTestService(
            Options.Create(botOptions),
            new FakeResolver(channel),
            new RecordingLogger<EventWatchDeliveryTestService>());
        var handler = new EventWatchCommandHandler(
            statusService,
            new EventWatchStatusMessageComposer(),
            testService,
            new RecordingLogger<EventWatchCommandHandler>());
        var interaction = new TestInteraction(EventWatchCommandNames.StatusSubcommand);
        var checkpointStore = new InMemoryCheckpointStore();

        await handler.HandleAsync(interaction);

        interaction.Deferred.Should().BeTrue();
        interaction.Response.Should().NotBeNullOrWhiteSpace();
        interaction.Response.Should().Contain("EventWatch status");
        interaction.Response.Should().NotContain("<html>");
        channel.Sent.Should().BeEmpty();
        checkpointStore.IsDelivered(
            new NotificationCheckpoint(10, 100, $"event-watch:{AtalantaWatchId}", NotificationTypes.EventWatchAnnouncement))
            .Should().BeFalse();
        checkpointStore.IsDelivered(
            new NotificationCheckpoint(10, 100, $"event-watch:{AtalantaWatchId}", NotificationTypes.EventWatchTicketLink))
            .Should().BeFalse();
    }

    [Test]
    public async Task HandleAsync_TestSubcommand_SendsWithoutEveryoneAndWithoutCheckpoints()
    {
        var observations = new RigaFcPageParser().Parse(
            "<html><body><div><article><h2>News</h2><p>Nothing.</p></article></div></body></html>",
            new Uri("https://rigafc.lv/"));
        var botOptions = CreateOptions(schedulingEnabled: true);
        var statusService = new EventWatchStatusService(
            new FixedSource(observations),
            new EventWatchSignalDetector(),
            Options.Create(botOptions),
            new RecordingLogger<EventWatchStatusService>());
        var channel = new FakeChannel();
        var testService = new EventWatchDeliveryTestService(
            Options.Create(botOptions),
            new FakeResolver(channel),
            new RecordingLogger<EventWatchDeliveryTestService>());
        var handler = new EventWatchCommandHandler(
            statusService,
            new EventWatchStatusMessageComposer(),
            testService,
            new RecordingLogger<EventWatchCommandHandler>());
        var interaction = new TestInteraction(EventWatchCommandNames.TestSubcommand);
        var checkpointStore = new InMemoryCheckpointStore();

        await handler.HandleAsync(interaction);

        var sent = channel.Sent.Should().ContainSingle().Subject;
        sent.AllowMention.Should().BeFalse();
        sent.Content.Should().NotContain("@everyone");
        sent.Content.Should().Contain("delivery test");

        interaction.Response.Should().Contain("delivery test");
        interaction.Response.Should().Contain("guild 10");
        checkpointStore.IsDelivered(
            new NotificationCheckpoint(10, 100, $"event-watch:{AtalantaWatchId}", NotificationTypes.EventWatchAnnouncement))
            .Should().BeFalse();
        checkpointStore.IsDelivered(
            new NotificationCheckpoint(10, 100, $"event-watch:{AtalantaWatchId}", NotificationTypes.EventWatchTicketLink))
            .Should().BeFalse();
    }

    [Test]
    public async Task HandleAsync_UnknownSubcommand_ShowsUsage()
    {
        var handler = CreateHandlerWithEmptyOptions();
        var interaction = new TestInteraction(subcommand: null);

        await handler.HandleAsync(interaction);

        interaction.Response.Should().Contain("/eventwatch status");
        interaction.Response.Should().Contain("/eventwatch test");
    }

    private static MandarinBotOptions CreateOptions(bool schedulingEnabled)
    {
        return new MandarinBotOptions
        {
            Schedules = new JobSchedulesOptions
            {
                EventWatch = new ScheduledJobOptions
                {
                    Enabled = schedulingEnabled,
                    Cron = "0 0/10 * * * ?"
                }
            },
            EventWatch = new EventWatchOptions
            {
                Watches =
                [
                    new EventWatchDefinition
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
                    }
                ]
            }
        };
    }

    private static EventWatchCommandHandler CreateHandlerWithEmptyOptions()
    {
        var botOptions = new MandarinBotOptions();
        return new EventWatchCommandHandler(
            new EventWatchStatusService(
                new FixedSource([]),
                new EventWatchSignalDetector(),
                Options.Create(botOptions),
                new RecordingLogger<EventWatchStatusService>()),
            new EventWatchStatusMessageComposer(),
            new EventWatchDeliveryTestService(
                Options.Create(botOptions),
                new FakeResolver(new FakeChannel()),
                new RecordingLogger<EventWatchDeliveryTestService>()),
            new RecordingLogger<EventWatchCommandHandler>());
    }

    private sealed class FixedSource(IReadOnlyList<EventWatchObservation> observations) : IEventWatchSource
    {
        public string SourceName => "test";

        public Task<IReadOnlyList<EventWatchObservation>> CollectAsync(CancellationToken cancellationToken) =>
            Task.FromResult(observations);
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

    private sealed class TestInteraction(string? subcommand) : IDiscordSlashCommandInteraction
    {
        public string Name => EventWatchCommandNames.CommandName;

        public string UserMention => "<@123>";

        public bool Deferred { get; private set; }

        public string Response { get; private set; } = string.Empty;

        public string? GetStringOption(string name) => null;

        public long? GetIntegerOption(string name) => null;

        public string? GetSubcommandName() => subcommand;

        public Task RespondAsync(string content)
        {
            Response = content;
            return Task.CompletedTask;
        }

        public Task DeferAsync()
        {
            Deferred = true;
            return Task.CompletedTask;
        }

        public Task ModifyOriginalResponseAsync(string content)
        {
            Response = content;
            return Task.CompletedTask;
        }

        public Task FollowupAsync(string content)
        {
            Response += "\n" + content;
            return Task.CompletedTask;
        }
    }
}
