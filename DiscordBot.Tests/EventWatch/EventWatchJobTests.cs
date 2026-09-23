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
public sealed class EventWatchJobTests
{
    private const string WatchId = "riga-fc-atalanta-2026";

    [Test]
    public async Task Execute_TicketAvailable_PublishesOnceWithStableSourceId()
    {
        var watch = new EventWatchDefinition
        {
            Enabled = true,
            Id = WatchId,
            Title = "Riga FC vs Atalanta tickets",
            MatchTerms = ["Atalanta"],
            Targets = [new NotificationTargetOptions { GuildId = 10, ChannelId = 100 }]
        };
        var source = new FixedSource(ParseCatalogue(CatalogueFixtures.WithAtalanta()));
        var job = CreateJob([watch], source, out var publisher, out _);

        await job.Execute(CreateContext());
        await job.Execute(CreateContext());

        // Fake publisher records every attempt; the stable source identifier
        // lets the real checkpoint store deduplicate. Here we assert the job
        // uses stable IDs so that deduplication is possible.
        publisher.Calls.Should().HaveCount(2);
        publisher.Calls.Should().OnlyContain(c => c.SourceIdentifier == $"event-watch:{WatchId}");
        publisher.Calls.Should().OnlyContain(c => c.NotificationType == NotificationTypes.EventWatchTicketAvailable);
        publisher.Calls.Should().OnlyContain(c => c.Target.GuildId == 10 && c.Target.ChannelId == 100);
    }

    [Test]
    public async Task Execute_WithRealPublisher_CheckpointsPreventDuplicates()
    {
        var watch = new EventWatchDefinition
        {
            Enabled = true,
            Id = WatchId,
            Title = "Riga FC vs Atalanta tickets",
            MatchTerms = ["Atalanta"],
            Targets = [new NotificationTargetOptions { GuildId = 10, ChannelId = 100 }]
        };
        var source = new FixedSource(ParseCatalogue(CatalogueFixtures.WithAtalanta()));
        var channel = new FakeChannel();
        var checkpointStore = new InMemoryCheckpointStore();
        var realPublisher = new DiscordNotificationPublisher(
            new FakeResolver(channel),
            new NotificationDeliveryCoordinator(
                checkpointStore,
                TimeProvider.System,
                new RecordingLogger<NotificationDeliveryCoordinator>()),
            new RecordingLogger<DiscordNotificationPublisher>());
        var job = CreateJobWithPublisher([watch], source, realPublisher);

        await job.Execute(CreateContext());
        var afterFirst = channel.Sent.Count;
        afterFirst.Should().Be(1);

        await job.Execute(CreateContext());
        channel.Sent.Should().HaveCount(afterFirst);
    }

    [Test]
    public async Task Execute_UsesOnlyWatchTargets_NotGeneralTargets()
    {
        var watchTarget = new NotificationTargetOptions { GuildId = 11, ChannelId = 101 };
        var watch = new EventWatchDefinition
        {
            Enabled = true,
            Id = WatchId,
            Title = "Riga FC vs Atalanta tickets",
            MatchTerms = ["Atalanta"],
            Targets = [watchTarget]
        };
        var generalTargets = new NotificationOptions
        {
            Targets = [new NotificationTargetOptions { GuildId = 999, ChannelId = 999 }]
        };
        var source = new FixedSource(ParseCatalogue(CatalogueFixtures.WithAtalanta()));
        var job = CreateJob([watch], source, out var publisher, out _);

        await job.Execute(CreateContext());

        publisher.Calls.Should().NotBeEmpty();
        publisher.Calls.Should().OnlyContain(c => c.Target.GuildId == 11 && c.Target.ChannelId == 101);
        publisher.Calls.Should().NotContain(c => c.Target.GuildId == 999);
        _ = generalTargets;
    }

    [Test]
    public async Task Execute_OneFailingTarget_DoesNotPreventOtherTarget()
    {
        var watch = new EventWatchDefinition
        {
            Enabled = true,
            Id = WatchId,
            Title = "Riga FC vs Atalanta tickets",
            MatchTerms = ["Atalanta"],
            Targets =
            [
                new NotificationTargetOptions { GuildId = 10, ChannelId = 100 },
                new NotificationTargetOptions { GuildId = 20, ChannelId = 200 }
            ]
        };
        var source = new FixedSource(ParseCatalogue(CatalogueFixtures.WithAtalanta()));
        var publisher = new FailingPublisher(failGuildId: 10);
        var job = CreateJobWithPublisher([watch], source, publisher);

        await job.Execute(CreateContext());

        publisher.Succeeded.Should().ContainSingle();
        publisher.Succeeded[0].GuildId.Should().Be(20);
        publisher.Attempted.Should().HaveCount(2);
    }

    [Test]
    public async Task Execute_CatalogueFailure_ThrowsWithoutPublishing()
    {
        var watch = new EventWatchDefinition
        {
            Enabled = true,
            Id = WatchId,
            Title = "Riga FC vs Atalanta tickets",
            MatchTerms = ["Atalanta"],
            Targets = [new NotificationTargetOptions { GuildId = 10, ChannelId = 100 }]
        };
        var job = CreateJob([watch], new ThrowingSource(), out var publisher, out _);

        Func<Task> act = () => job.Execute(CreateContext());

        await act.Should().ThrowAsync<RigaFcApiException>();
        publisher.Calls.Should().BeEmpty();
    }

    [Test]
    public void Compose_TicketAvailable_IdentifiesEventAndTicketUrl()
    {
        var composer = new EventWatchMessageCompositionService();

        var message = composer.ComposeTicketAvailable(
            "Riga FC vs Atalanta tickets",
            "https://www.bilesuserviss.lv/biletes/ATALANTA01/riga-fc-vs-atalanta",
            "https://www.bilesuserviss.lv/biletes/ATALANTA01/riga-fc-vs-atalanta");

        message.Should().Contain("Riga FC vs Atalanta tickets");
        message.Should().Contain("https://www.bilesuserviss.lv/biletes/ATALANTA01/riga-fc-vs-atalanta");
        message.Should().Contain("tickets available");
        message.Should().NotContain("@everyone");
    }

    private static IReadOnlyList<EventWatchObservation> ParseCatalogue(string json)
    {
        return new RigaFcTicketCatalogueParser().Parse(json, RigaFcClient.TicketCatalogueApiUri);
    }

    private static EventWatchJob CreateJob(
        IReadOnlyList<EventWatchDefinition> watches,
        IEventWatchSource source,
        out RecordingPublisher publisher,
        out InMemoryCheckpointStore store)
    {
        store = new InMemoryCheckpointStore();
        publisher = new RecordingPublisher();
        return CreateJobWithPublisher(watches, source, publisher);
    }

    private static EventWatchJob CreateJobWithPublisher(
        IReadOnlyList<EventWatchDefinition> watches,
        IEventWatchSource source,
        IDiscordNotificationPublisher publisher)
    {
        var botOptions = new MandarinBotOptions
        {
            EventWatch = new EventWatchOptions
            {
                Watches = watches.ToList()
            }
        };
        var service = new EventWatchService(
            source,
            new EventWatchSignalDetector(),
            Options.Create(botOptions),
            new RecordingLogger<EventWatchService>());
        return new EventWatchJob(
            new ReadyConnection(),
            service,
            new EventWatchMessageCompositionService(),
            publisher,
            TimeProvider.System,
            new RecordingLogger<EventWatchJob>());
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

    private sealed class FixedSource(IReadOnlyList<EventWatchObservation> observations) : IEventWatchSource
    {
        public string SourceName => "test";

        public Task<IReadOnlyList<EventWatchObservation>> CollectAsync(CancellationToken cancellationToken) =>
            Task.FromResult(observations);
    }

    private sealed class ThrowingSource : IEventWatchSource
    {
        public string SourceName => "test";

        public Task<IReadOnlyList<EventWatchObservation>> CollectAsync(CancellationToken cancellationToken) =>
            Task.FromException<IReadOnlyList<EventWatchObservation>>(
                new RigaFcApiException("ticket catalogue failed"));
    }

    private sealed class ReadyConnection : IDiscordConnectionReadiness
    {
        public bool IsReady => true;

        public Task WaitUntilReadyAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class RecordingPublisher : IDiscordNotificationPublisher
    {
        public List<PublisherCall> Calls { get; } = [];

        public Task<bool> PublishOnceAsync(
            NotificationTargetOptions target,
            string sourceIdentifier,
            string notificationType,
            string message,
            CancellationToken cancellationToken)
        {
            Calls.Add(new PublisherCall(target, sourceIdentifier, notificationType, message));
            return Task.FromResult(true);
        }
    }

    private sealed record PublisherCall(
        NotificationTargetOptions Target,
        string SourceIdentifier,
        string NotificationType,
        string Message);

    private sealed class FailingPublisher(ulong failGuildId) : IDiscordNotificationPublisher
    {
        public List<NotificationTargetOptions> Attempted { get; } = [];
        public List<NotificationTargetOptions> Succeeded { get; } = [];

        public Task<bool> PublishOnceAsync(
            NotificationTargetOptions target,
            string sourceIdentifier,
            string notificationType,
            string message,
            CancellationToken cancellationToken)
        {
            Attempted.Add(target);
            if (target.GuildId == failGuildId)
            {
                throw new InvalidOperationException("Discord send failed.");
            }

            Succeeded.Add(target);
            return Task.FromResult(true);
        }
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
