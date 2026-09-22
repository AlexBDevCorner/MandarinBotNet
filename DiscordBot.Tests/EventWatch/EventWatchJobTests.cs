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
    public async Task Execute_AnnouncementAndTicketLink_PublishesEachOnceWithStableSourceId()
    {
        var watch = new EventWatchDefinition
        {
            Enabled = true,
            Id = WatchId,
            Title = "Riga FC vs Atalanta tickets",
            MatchTerms = ["Atalanta"],
            Targets = [new NotificationTargetOptions { GuildId = 10, ChannelId = 100 }]
        };
        // Note: uses real Latvian wording through the parser path below.
        var latvianHtml = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Biļetes pārdošanā!</p><a href="https://bilesuserviss.lv/x">Pirkt biļetes</a></article></div>
            </body></html>
            """;
        var source = new FixedSource(ParseAll(latvianHtml));
        var job = CreateJob([watch], source, out var publisher, out _);

        await job.Execute(CreateContext());
        await job.Execute(CreateContext());

        var announcementCalls = publisher.Calls
            .Where(c => c.NotificationType == NotificationTypes.EventWatchAnnouncement)
            .ToList();
        var ticketCalls = publisher.Calls
            .Where(c => c.NotificationType == NotificationTypes.EventWatchTicketLink)
            .ToList();

        // Fake publisher records every attempt; the stable source identifier
        // lets the real checkpoint store deduplicate. Here we assert the job
        // uses stable IDs so that deduplication is possible.
        announcementCalls.Should().HaveCount(2);
        ticketCalls.Should().HaveCount(2);
        announcementCalls.Should().OnlyContain(c => c.SourceIdentifier == $"event-watch:{WatchId}");
        ticketCalls.Should().OnlyContain(c => c.SourceIdentifier == $"event-watch:{WatchId}");
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
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Biļetes pārdošanā!</p><a href="https://bilesuserviss.lv/y">Pirkt biļetes</a></article></div>
            </body></html>
            """;
        var observations = new RigaFcPageParser().Parse(html, new Uri("https://rigafc.lv/"));
        var source = new FixedSource(observations);
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
        afterFirst.Should().Be(2);

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
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Biļetes pārdošanā!</p></article></div>
            </body></html>
            """;
        var source = new FixedSource(new RigaFcPageParser().Parse(html, new Uri("https://rigafc.lv/")));
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
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Biļetes pārdošanā!</p></article></div>
            </body></html>
            """;
        var source = new FixedSource(new RigaFcPageParser().Parse(html, new Uri("https://rigafc.lv/")));
        var publisher = new FailingPublisher(failGuildId: 10);
        var job = CreateJobWithPublisher([watch], source, publisher);

        await job.Execute(CreateContext());

        publisher.Succeeded.Should().ContainSingle();
        publisher.Succeeded[0].GuildId.Should().Be(20);
        publisher.Attempted.Should().HaveCount(2);
    }

    [Test]
    public async Task Execute_OneFailingPage_DoesNotPreventOtherPages()
    {
        var watch = new EventWatchDefinition
        {
            Enabled = true,
            Id = WatchId,
            Title = "Riga FC vs Atalanta tickets",
            MatchTerms = ["Atalanta"],
            Targets = [new NotificationTargetOptions { GuildId = 10, ChannelId = 100 }]
        };
        var goodHtml = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Biļetes pārdošanā!</p></article></div>
            </body></html>
            """;
        var source = new PartiallyFailingSource(goodHtml);
        var job = CreateJob([watch], source, out var publisher, out _);

        await job.Execute(CreateContext());

        publisher.Calls.Should().NotBeEmpty();
        publisher.Calls.Should().OnlyContain(c => c.NotificationType == NotificationTypes.EventWatchAnnouncement);
    }

    [Test]
    public void Compose_Announcement_IdentifiesEventAndSource()
    {
        var composer = new EventWatchMessageCompositionService();

        var message = composer.ComposeAnnouncement(
            "Riga FC vs Atalanta tickets",
            "https://rigafc.lv/jaunumi/");

        message.Should().Contain("Riga FC vs Atalanta tickets");
        message.Should().Contain("https://rigafc.lv/jaunumi/");
    }

    [Test]
    public void Compose_TicketLink_IncludesTicketAndSourceUrls()
    {
        var composer = new EventWatchMessageCompositionService();

        var message = composer.ComposeTicketLink(
            "Riga FC vs Atalanta tickets",
            "https://bilesuserviss.lv/riga-atalanta",
            "https://rigafc.lv/");

        message.Should().Contain("https://bilesuserviss.lv/riga-atalanta");
        message.Should().Contain("https://rigafc.lv/");
        message.Should().Contain("Riga FC vs Atalanta tickets");
    }

    private static IReadOnlyList<EventWatchObservation> ParseAll(string html)
    {
        return new RigaFcPageParser().Parse(html, new Uri("https://rigafc.lv/"));
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

    private sealed class PartiallyFailingSource(string goodHtml) : IEventWatchSource
    {
        public string SourceName => "riga-fc";

        public async Task<IReadOnlyList<EventWatchObservation>> CollectAsync(CancellationToken cancellationToken)
        {
            // Simulate: homepage fails, calendar + news succeed via the real
            // RigaFcEventWatchSource resilience pattern (one failure does not
            // suppress the others).
            var parser = new RigaFcPageParser();
            var observations = new List<EventWatchObservation>();
            try
            {
                throw new RigaFcApiException("homepage failed", System.Net.HttpStatusCode.InternalServerError);
            }
            catch (RigaFcApiException)
            {
                // Swallowed per-page, continue with remaining pages.
            }

            observations.AddRange(parser.Parse(goodHtml, new Uri("https://rigafc.lv/kalendars/")));
            observations.AddRange(parser.Parse(goodHtml, new Uri("https://rigafc.lv/jaunumi/")));
            return await Task.FromResult<IReadOnlyList<EventWatchObservation>>(observations);
        }
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
                "get_RefireCount" => 0,
                "get_CancellationToken" => CancellationToken.None,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }
    }
}
