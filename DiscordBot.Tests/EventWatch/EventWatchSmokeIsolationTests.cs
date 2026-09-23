using System.Reflection;
using AwesomeAssertions;
using DiscordBot.EventWatch;
using DiscordBot.Jobs;
using DiscordBot.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Quartz;

namespace DiscordBot.Tests.EventWatch;

[TestFixture]
public sealed class EventWatchSmokeIsolationTests
{
    private const string AtalantaWatchId = "riga-fc-atalanta-2026";
    private const string SmokeWatchId = "riga-fc-smoke-kairat-2026";

    [Test]
    public void SmokeAndProductionCheckpoints_AreDifferentSourceIdentifiers()
    {
        var smokeSourceId = EventWatchSourceIdentifier.Create(SmokeWatchId);
        var productionSourceId = EventWatchSourceIdentifier.Create(AtalantaWatchId);

        smokeSourceId.Should().NotBe(productionSourceId);
        smokeSourceId.Should().Be("event-watch:riga-fc-smoke-kairat-2026");
        productionSourceId.Should().Be("event-watch:riga-fc-atalanta-2026");
    }

    [Test]
    public async Task SmokeDelivery_DoesNotConsumeProductionCheckpoints()
    {
        var harness = CreateHarness(KairatTicketHtml());
        var smokeSourceId = EventWatchSourceIdentifier.Create(SmokeWatchId);
        var productionSourceId = EventWatchSourceIdentifier.Create(AtalantaWatchId);

        await harness.Job.Execute(CreateContext());

        // Smoke watch delivered both signal kinds through the scheduled path.
        harness.Channel.Sent.Should().HaveCount(2);
        harness.Store.IsDelivered(
            new NotificationCheckpoint(10, 100, smokeSourceId, NotificationTypes.EventWatchAnnouncement))
            .Should().BeTrue();
        harness.Store.IsDelivered(
            new NotificationCheckpoint(10, 100, smokeSourceId, NotificationTypes.EventWatchTicketLink))
            .Should().BeTrue();

        // Production Atalanta checkpoints remain independently deliverable.
        harness.Store.IsDelivered(
            new NotificationCheckpoint(10, 100, productionSourceId, NotificationTypes.EventWatchAnnouncement))
            .Should().BeFalse();
        harness.Store.IsDelivered(
            new NotificationCheckpoint(10, 100, productionSourceId, NotificationTypes.EventWatchTicketLink))
            .Should().BeFalse();
    }

    [Test]
    public async Task UnchangedSmokeSignal_IsDeliveredOnlyOnce()
    {
        var harness = CreateHarness(KairatTicketHtml());

        await harness.Job.Execute(CreateContext());
        var afterFirst = harness.Channel.Sent.Count;
        afterFirst.Should().Be(2);

        await harness.Job.Execute(CreateContext());

        harness.Channel.Sent.Should().HaveCount(afterFirst);
    }

    [Test]
    public async Task AfterSmokeDelivery_AtalantaRemainsIndependentlyDeliverable()
    {
        var kairatObservations = new RigaFcPageParser().Parse(
            KairatTicketHtml(),
            new Uri("https://rigafc.lv/kalendars/"));
        var atalantaObservations = new RigaFcPageParser().Parse(
            AtalantaTicketHtml(),
            new Uri("https://rigafc.lv/kalendars/"));
        var source = new MutableSource(kairatObservations);
        var harness = CreateHarness(source);

        await harness.Job.Execute(CreateContext());
        harness.Channel.Sent.Should().HaveCount(2);

        source.SetObservations(atalantaObservations);
        await harness.Job.Execute(CreateContext());

        // Atalanta announcement + ticket-link arrive in addition to smoke.
        harness.Channel.Sent.Should().HaveCount(4);
        var productionSourceId = EventWatchSourceIdentifier.Create(AtalantaWatchId);
        harness.Store.IsDelivered(
            new NotificationCheckpoint(10, 100, productionSourceId, NotificationTypes.EventWatchAnnouncement))
            .Should().BeTrue();
        harness.Store.IsDelivered(
            new NotificationCheckpoint(10, 100, productionSourceId, NotificationTypes.EventWatchTicketLink))
            .Should().BeTrue();
    }

    [Test]
    public async Task ScheduledSmokeWatch_WithKairatEntry_ProducesQualifyingNotification()
    {
        var harness = CreateHarness(KairatTicketHtml());

        await harness.Job.Execute(CreateContext());

        harness.Channel.Sent.Should().NotBeEmpty();
        harness.Channel.Sent.Should().OnlyContain(sent => sent.Content.Contains("Kairat"));
    }

    private static string KairatTicketHtml() => """
        <html><body>
        <div><article><h2>Riga vs Kairat Almaty</h2><p>15 October 2026, 19:45, Skonto stadions. Biļetes pārdošanā uz šo spēli.</p><a href="https://bilesuserviss.lv/kairat-riga">Pirkt biļetes</a></article></div>
        </body></html>
        """;

    private static string AtalantaTicketHtml() => """
        <html><body>
        <div><article><h2>Riga FC vs Atalanta</h2><p>Biļetes pārdošanā uz Riga FC vs Atalanta spēli.</p><a href="https://bilesuserviss.lv/riga-atalanta">Pirkt biļetes</a></article></div>
        </body></html>
        """;

    private static Harness CreateHarness(string html)
    {
        var observations = new RigaFcPageParser().Parse(html, new Uri("https://rigafc.lv/kalendars/"));
        return CreateHarness(new MutableSource(observations));
    }

    private static Harness CreateHarness(MutableSource source)
    {
        var watches = new List<EventWatchDefinition>
        {
            new()
            {
                Enabled = true,
                Id = AtalantaWatchId,
                Title = "Riga FC vs Atalanta tickets",
                MatchTerms = ["Atalanta"],
                Targets = [new NotificationTargetOptions { GuildId = 10, ChannelId = 100 }]
            },
            new()
            {
                Enabled = true,
                Id = SmokeWatchId,
                Title = "[SMOKE TEST] Riga FC vs Kairat tickets",
                MatchTerms = ["Kairat"],
                Targets = [new NotificationTargetOptions { GuildId = 10, ChannelId = 100, MentionEveryone = false }]
            }
        };
        var botOptions = new MandarinBotOptions
        {
            EventWatch = new EventWatchOptions { Watches = watches }
        };
        var service = new EventWatchService(
            source,
            new EventWatchSignalDetector(),
            Options.Create(botOptions),
            new RecordingLogger<EventWatchService>());
        var channel = new FakeChannel();
        var store = new InMemoryCheckpointStore();
        var publisher = new DiscordNotificationPublisher(
            new FakeResolver(channel),
            new NotificationDeliveryCoordinator(
                store,
                TimeProvider.System,
                new RecordingLogger<NotificationDeliveryCoordinator>()),
            new RecordingLogger<DiscordNotificationPublisher>());
        var job = new EventWatchJob(
            new ReadyConnection(),
            service,
            new EventWatchMessageCompositionService(),
            publisher,
            TimeProvider.System,
            new RecordingLogger<EventWatchJob>());
        return new Harness(job, channel, store);
    }

    private static IJobExecutionContext CreateContext()
    {
        var context = DispatchProxy.Create<IJobExecutionContext, JobContextProxy>();
        var proxy = (JobContextProxy)(object)context;
        proxy.JobDetail = JobBuilder.Create<EventWatchJob>()
            .WithIdentity("event-watch", "jobs")
            .Build();
        proxy.FireInstanceId = Guid.NewGuid().ToString("N");
        return context;
    }

    private sealed record Harness(
        EventWatchJob Job,
        FakeChannel Channel,
        InMemoryCheckpointStore Store);

    private sealed class MutableSource(IReadOnlyList<EventWatchObservation> observations) : IEventWatchSource
    {
        private IReadOnlyList<EventWatchObservation> _observations = observations;

        public string SourceName => "test";

        public void SetObservations(IReadOnlyList<EventWatchObservation> observations) =>
            _observations = observations;

        public Task<IReadOnlyList<EventWatchObservation>> CollectAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_observations);
    }

    private sealed class ReadyConnection : IDiscordConnectionReadiness
    {
        public bool IsReady => true;

        public Task WaitUntilReadyAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
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

    public class JobContextProxy : DispatchProxy
    {
        public required IJobDetail JobDetail { get; set; }

        public required string FireInstanceId { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_JobDetail" => JobDetail,
                "get_FireInstanceId" => FireInstanceId,
                "get_CancellationToken" => CancellationToken.None,
                "get_RefireCount" => 0,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }
    }
}
