using System.Net;
using System.Reflection;
using System.Text;
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
public sealed class EventWatchVerticalSliceTests
{
    private const string WatchId = "riga-fc-atalanta-2026";

    [Test]
    public async Task Sequence_NoSale_Announcement_Unchanged_TicketLink_Unchanged_DeliversOnceEach()
    {
        var responses = new MutableRigaFcHandler();
        var httpClient = new HttpClient(responses)
        {
            BaseAddress = new Uri("https://rigafc.lv/")
        };
        var rigaClient = new RigaFcClient(httpClient, new RecordingLogger<RigaFcClient>());
        var parser = new RigaFcPageParser();
        var sourceLogger = new RecordingLogger<RigaFcEventWatchSource>();
        var source = new RigaFcEventWatchSource(rigaClient, parser, sourceLogger);

        var watch = new EventWatchDefinition
        {
            Enabled = true,
            Id = WatchId,
            Title = "Riga FC vs Atalanta tickets",
            MatchTerms = ["Atalanta"],
            Targets = [new NotificationTargetOptions { GuildId = 10, ChannelId = 100 }]
        };
        var botOptions = new MandarinBotOptions
        {
            EventWatch = new EventWatchOptions { Watches = [watch] }
        };
        var service = new EventWatchService(
            source,
            new EventWatchSignalDetector(),
            Options.Create(botOptions),
            new RecordingLogger<EventWatchService>());

        var channel = new FakeChannel();
        var checkpointStore = new InMemoryCheckpointStore();
        var publisher = new DiscordNotificationPublisher(
            new FakeResolver(channel),
            new NotificationDeliveryCoordinator(
                checkpointStore,
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

        // 1. No sale information -> no notification.
        responses.SetAll(NoSaleHtml());
        await job.Execute(CreateContext());
        channel.Sent.Should().BeEmpty();

        // 2. Sale announcement appears -> one Announcement.
        responses.SetAll(AnnouncementHtml());
        await job.Execute(CreateContext());
        channel.Sent.Should().ContainSingle();
        channel.Sent[0].Content.Should().Contain("Riga FC vs Atalanta tickets");
        channel.Sent[0].Content.Should().Contain("https://rigafc.lv/");
        var announcementType = NotificationTypes.EventWatchAnnouncement;
        checkpointStore.IsDelivered(
            new NotificationCheckpoint(10, 100, $"event-watch:{WatchId}", announcementType))
            .Should().BeTrue();

        // 3. Unchanged page -> no duplicate.
        await job.Execute(CreateContext());
        channel.Sent.Should().ContainSingle();

        // 4. Direct ticket link appears -> one TicketLinkAvailable.
        responses.SetAll(TicketLinkHtml());
        await job.Execute(CreateContext());
        channel.Sent.Should().HaveCount(2);
        channel.Sent[1].Content.Should().Contain("https://bilesuserviss.lv/riga-atalanta");
        channel.Sent[1].Content.Should().Contain("https://rigafc.lv/");
        var ticketType = NotificationTypes.EventWatchTicketLink;
        checkpointStore.IsDelivered(
            new NotificationCheckpoint(10, 100, $"event-watch:{WatchId}", ticketType))
            .Should().BeTrue();

        // 5. Unchanged page -> no duplicate.
        await job.Execute(CreateContext());
        channel.Sent.Should().HaveCount(2);

        // No raw HTML should be logged.
        sourceLogger.Entries.Should().OnlyContain(entry =>
            entry.Message == null || !entry.Message.Contains("<html>"));
    }

    private static string NoSaleHtml() => """
        <html><body>
        <div><article><h2>Riga FC vs Atalanta</h2><p>Fixture on Saturday at Skonto Stadium at 17:00.</p></article></div>
        </body></html>
        """;

    private static string AnnouncementHtml() => """
        <html><body>
        <div><article><h2>Riga FC vs Atalanta</h2><p>Biļetes jau pārdošanā! Iegādāties biļetes uz šo spēli.</p></article></div>
        </body></html>
        """;

    private static string TicketLinkHtml() => """
        <html><body>
        <div><article><h2>Riga FC vs Atalanta</h2><p>Biļetes pārdošanā uz Riga FC vs Atalanta spēli.</p><a href="https://bilesuserviss.lv/riga-atalanta">Pirkt biļetes</a></article></div>
        </body></html>
        """;

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

    private sealed class MutableRigaFcHandler : HttpMessageHandler
    {
        private string _html = "<html></html>";

        public void SetAll(string html) => _html = html;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_html, Encoding.UTF8, "text/html")
            };
            return Task.FromResult(response);
        }
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
