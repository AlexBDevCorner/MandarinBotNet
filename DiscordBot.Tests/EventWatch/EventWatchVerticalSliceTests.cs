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
    public async Task Sequence_Empty_Package_Atalanta_Unchanged_KairatOnly_DeliversOnce()
    {
        var responses = new MutableCatalogueHandler();
        var httpClient = new HttpClient(responses)
        {
            BaseAddress = new Uri("https://www.bilesuserviss.lv/")
        };
        var rigaClient = new RigaFcClient(httpClient, new RecordingLogger<RigaFcClient>());
        var parser = new RigaFcTicketCatalogueParser();
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

        // 1. Empty catalogue -> no notification.
        responses.SetPayload(CatalogueFixtures.Empty());
        await job.Execute(CreateContext());
        channel.Sent.Should().BeEmpty();

        // 2. Generic Conference League package -> no opponent-specific signal.
        responses.SetPayload(CatalogueFixtures.GenericPackage());
        await job.Execute(CreateContext());
        channel.Sent.Should().BeEmpty();

        // 3. Atalanta product appears -> exactly one ticket-available notification.
        responses.SetPayload(CatalogueFixtures.WithAtalanta());
        await job.Execute(CreateContext());
        channel.Sent.Should().ContainSingle();
        channel.Sent[0].Content.Should().Contain("Riga FC vs Atalanta tickets");
        channel.Sent[0].Content.Should().Contain("https://www.bilesuserviss.lv/biletes/ATALANTA01/riga-fc-vs-atalanta");
        checkpointStore.IsDelivered(
            new NotificationCheckpoint(10, 100, $"event-watch:{WatchId}", NotificationTypes.EventWatchTicketAvailable))
            .Should().BeTrue();

        // 4. Unchanged catalogue -> no duplicate.
        await job.Execute(CreateContext());
        channel.Sent.Should().ContainSingle();

        // 5. Kairat-only catalogue product does not trigger the Atalanta watch.
        responses.SetPayload(CatalogueFixtures.WithKairat());
        await job.Execute(CreateContext());
        channel.Sent.Should().ContainSingle();
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

    private sealed class MutableCatalogueHandler : HttpMessageHandler
    {
        private string _payload = CatalogueFixtures.Empty();

        public void SetPayload(string payload) => _payload = payload;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.RequestUri.Should().NotBeNull();
            request.RequestUri!.ToString().Should().Contain("bilesuserviss.lv");
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_payload, Encoding.UTF8, "application/json")
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
