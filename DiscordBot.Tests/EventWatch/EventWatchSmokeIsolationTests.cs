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
    private const string KairatWatchId = "riga-fc-smoke-kairat-2026";

    [Test]
    public void SmokeAndProductionCheckpoints_AreDifferentSourceIdentifiers()
    {
        var smokeSourceId = EventWatchSourceIdentifier.Create(KairatWatchId);
        var productionSourceId = EventWatchSourceIdentifier.Create(AtalantaWatchId);

        smokeSourceId.Should().NotBe(productionSourceId);
        smokeSourceId.Should().Be("event-watch:riga-fc-smoke-kairat-2026");
        productionSourceId.Should().Be("event-watch:riga-fc-atalanta-2026");
    }

    [Test]
    public async Task SmokeDelivery_DoesNotConsumeProductionCheckpoints()
    {
        var harness = CreateHarness(CatalogueFixtures.WithKairat());
        var smokeSourceId = EventWatchSourceIdentifier.Create(KairatWatchId);
        var productionSourceId = EventWatchSourceIdentifier.Create(AtalantaWatchId);

        await harness.Job.Execute(CreateContext());

        // Kairat watch delivered exactly one ticket-available signal.
        harness.Channel.Sent.Should().ContainSingle();
        harness.Store.IsDelivered(
            new NotificationCheckpoint(10, 100, smokeSourceId, NotificationTypes.EventWatchTicketAvailable))
            .Should().BeTrue();

        // Production Atalanta checkpoints remain independently deliverable.
        harness.Store.IsDelivered(
            new NotificationCheckpoint(10, 100, productionSourceId, NotificationTypes.EventWatchTicketAvailable))
            .Should().BeFalse();
    }

    [Test]
    public async Task UnchangedSmokeSignal_IsDeliveredOnlyOnce()
    {
        var harness = CreateHarness(CatalogueFixtures.WithKairat());

        await harness.Job.Execute(CreateContext());
        var afterFirst = harness.Channel.Sent.Count;
        afterFirst.Should().Be(1);

        await harness.Job.Execute(CreateContext());

        harness.Channel.Sent.Should().HaveCount(afterFirst);
    }

    [Test]
    public async Task AfterSmokeDelivery_AtalantaRemainsIndependentlyDeliverable()
    {
        var parser = new RigaFcTicketCatalogueParser();
        var kairatObservations = parser.Parse(
            CatalogueFixtures.WithKairat(),
            RigaFcClient.TicketCatalogueApiUri);
        var atalantaObservations = parser.Parse(
            CatalogueFixtures.WithAtalanta(),
            RigaFcClient.TicketCatalogueApiUri);
        var source = new MutableSource(kairatObservations);
        var harness = CreateHarness(source);

        await harness.Job.Execute(CreateContext());
        harness.Channel.Sent.Should().ContainSingle();

        source.SetObservations(atalantaObservations);
        await harness.Job.Execute(CreateContext());

        // Atalanta ticket-available arrives in addition to the earlier Kairat one.
        harness.Channel.Sent.Should().HaveCount(2);
        var productionSourceId = EventWatchSourceIdentifier.Create(AtalantaWatchId);
        harness.Store.IsDelivered(
            new NotificationCheckpoint(10, 100, productionSourceId, NotificationTypes.EventWatchTicketAvailable))
            .Should().BeTrue();
    }

    [Test]
    public async Task ScheduledKairatWatch_WithKairatProduct_ProducesQualifyingNotification()
    {
        var harness = CreateHarness(CatalogueFixtures.WithKairat());

        await harness.Job.Execute(CreateContext());

        harness.Channel.Sent.Should().NotBeEmpty();
        harness.Channel.Sent.Should().OnlyContain(sent => sent.Content.Contains("Kairat"));
    }

    private static Harness CreateHarness(string catalogueJson)
    {
        var observations = new RigaFcTicketCatalogueParser().Parse(
            catalogueJson,
            RigaFcClient.TicketCatalogueApiUri);
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
                Id = KairatWatchId,
                Title = "Riga FC vs Kairat tickets",
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
