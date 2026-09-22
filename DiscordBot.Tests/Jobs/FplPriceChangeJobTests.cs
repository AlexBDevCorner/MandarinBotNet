using System.Reflection;
using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.PriceChanges;
using DiscordBot.Jobs;
using DiscordBot.Notifications;
using DiscordBot.Responses;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Quartz;

namespace DiscordBot.Tests.Jobs;

[TestFixture]
public sealed class FplPriceChangeJobTests
{
    [Test]
    public async Task Execute_BaselineThenPriceChange_PublishesOnceForTheChange()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient();
        var store = new InMemoryFplPriceSnapshotStore();
        var publisher = new TestNotificationPublisher();
        var job = CreateJob(client, store, publisher);
        var context = CreateContext();

        // Act
        await job.Execute(context);
        client.Bootstrap = CreateBootstrap(101);
        await job.Execute(context);
        await job.Execute(context);

        // Assert
        publisher.Publications.Should().ContainSingle();
        publisher.Publications[0].NotificationType.Should()
            .Be(NotificationTypes.FplPriceChanges);
        publisher.Publications[0].Message.Should().Contain("+0,1 млн £");
        store.Prices.Should().BeEquivalentTo(new Dictionary<int, int> { [1] = 101 });
    }

    [Test]
    public async Task Execute_SkippedDestination_KeepsPriceChangeForRetry()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = CreateBootstrap(101)
        };
        var store = new InMemoryFplPriceSnapshotStore();
        store.SaveSnapshot(new Dictionary<int, int> { [1] = 100 });
        var publisher = new TestNotificationPublisher { SkipPublication = true };
        var job = CreateJob(client, store, publisher);
        var context = CreateContext();

        // Act
        await job.Execute(context);
        publisher.SkipPublication = false;
        await job.Execute(context);

        // Assert
        publisher.Publications.Should().ContainSingle();
        store.Prices.Should().BeEquivalentTo(new Dictionary<int, int> { [1] = 101 });
    }

    [Test]
    public async Task Execute_MixedOwnedAndUnowned_PublishesCompleteMessage()
    {
        // Arrange: Saka unowned decrease, Salah owned increase.
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = CreateLeagueBootstrap(),
            Managers = [new ClassicStanding { Entry = 1, EntryName = "Bobrov FC" }]
        };
        client.Picks[1] = new EntryEventPicksResponse
        {
            Picks = [new EntryEventPick { Element = 10 }]
        };
        var store = new InMemoryFplPriceSnapshotStore();
        store.SaveSnapshot(new Dictionary<int, int> { [10] = 100, [30] = 100 });
        var publisher = new TestNotificationPublisher();
        var job = CreateJob(
            client,
            store,
            publisher,
            new FantasyPremierLeagueOptions { ClassicLeagueId = 123 });
        var context = CreateContext();

        // Act
        await job.Execute(context);

        // Assert: both changes visible, only owned annotated, totals exclude unowned.
        publisher.Publications.Should().ContainSingle();
        var message = publisher.Publications[0].Message;
        message.Should().Contain("Saka");
        message.Should().Contain("Salah");
        message.Should().Contain("📉 Saka");
        message.Should().Contain("📈 Salah");
        message.Should().Contain("В составах: Bobrov FC");
        message.Should().Contain("💸 Общая стоимость составов: +0,1 млн £");
        message.Should().Contain("📋 По командам:");
    }

    [Test]
    public async Task Execute_LargeBatch_PublishesCompleteMessageWithoutTruncation()
    {
        // Arrange: synthetic batch longer than a single Discord message.
        const int playerCount = 150;
        var elements = Enumerable.Range(1, playerCount)
            .Select(id => new PremierLeagueElement
            {
                Id = id,
                WebName = $"Player{id:D3}",
                NowCost = 101
            })
            .ToList();
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = new BootstrapStaticResponse { Elements = elements }
        };
        var store = new InMemoryFplPriceSnapshotStore();
        store.SaveSnapshot(elements.ToDictionary(element => element.Id, _ => 100));
        var publisher = new TestNotificationPublisher();
        var job = CreateJob(client, store, publisher);
        var context = CreateContext();

        // Act
        await job.Execute(context);

        // Assert: full message retained and chunkable without dropping players.
        publisher.Publications.Should().ContainSingle();
        var message = publisher.Publications[0].Message;
        message.Length.Should().BeGreaterThan(2_000);
        var chunks = DiscordBot.Notifications.DiscordMessageChunker.Split(message, 2_000);
        chunks.Should().HaveCountGreaterThan(1);
        var combined = string.Join('\n', chunks);
        foreach (var element in elements)
        {
            combined.Should().Contain(element.WebName);
        }
    }

    private static FplPriceChangeJob CreateJob(
        TestFantasyPremierLeagueClient client,
        InMemoryFplPriceSnapshotStore store,
        TestNotificationPublisher publisher,
        FantasyPremierLeagueOptions? leagueOptions = null)
    {
        return new FplPriceChangeJob(
            new ReadyDiscordConnection(),
            new FplPriceChangeService(
                client,
                store,
                TimeProvider.System,
                new RecordingLogger<FplPriceChangeService>()),
            new FplLeaguePriceChangeService(
                client,
                leagueOptions ?? new FantasyPremierLeagueOptions(),
                new RecordingLogger<FplLeaguePriceChangeService>()),
            new FplPriceChangeMessageCompositionService(),
            publisher,
            new NotificationOptions
            {
                Targets =
                [
                    new NotificationTargetOptions
                    {
                        GuildId = 10,
                        ChannelId = 100
                    }
                ]
            },
            TimeProvider.System,
            new RecordingLogger<FplPriceChangeJob>());
    }

    private static IJobExecutionContext CreateContext()
    {
        var context = DispatchProxy.Create<
            IJobExecutionContext,
            JobExecutionContextProxy>();
        var proxy = (JobExecutionContextProxy)(object)context;
        proxy.JobDetail = JobBuilder
            .Create<FplPriceChangeJob>()
            .WithIdentity("fpl-price-changes", "jobs")
            .Build();
        proxy.FireInstanceId = "fire-123";
        return context;
    }

    private static BootstrapStaticResponse CreateBootstrap(int cost)
    {
        return new BootstrapStaticResponse
        {
            Elements =
            [
                new PremierLeagueElement
                {
                    Id = 1,
                    WebName = "Salah",
                    NowCost = cost
                }
            ]
        };
    }

    private static BootstrapStaticResponse CreateLeagueBootstrap()
    {
        return new BootstrapStaticResponse
        {
            Events =
            [
                new PremierLeagueEvent
                {
                    Id = 3,
                    IsCurrent = true
                }
            ],
            Elements =
            [
                new PremierLeagueElement
                {
                    Id = 10,
                    WebName = "Salah",
                    NowCost = 101
                },
                new PremierLeagueElement
                {
                    Id = 30,
                    WebName = "Saka",
                    NowCost = 99
                }
            ]
        };
    }

    private sealed class TestFantasyPremierLeagueClient : IFantasyPremierLeagueClient
    {
        public BootstrapStaticResponse Bootstrap { get; set; } = CreateBootstrap(100);

        public List<ClassicStanding> Managers { get; init; } = [];

        public Dictionary<int, EntryEventPicksResponse> Picks { get; } = [];

        public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Bootstrap);
        }

        public Task<ClassicStandingsResponse> GetClassicStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ClassicStandingsResponse
            {
                Standings = new ClassicStandings { Results = Managers }
            });
        }

        public Task<HeadToHeadStandingsResponse> GetHeadToHeadStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<EntryEventPicksResponse> GetEntryEventPicksAsync(
            int entryId,
            int eventId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Picks[entryId]);
        }

        public Task<EventLiveResponse> GetEventLiveAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<PremierLeagueFixture>> GetFixturesAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class InMemoryFplPriceSnapshotStore : IFplPriceSnapshotStore
    {
        public Dictionary<int, int> Prices { get; } = [];

        public FplPriceChangeBatch? LatestChanges { get; private set; }

        public FplPriceSnapshot? GetSnapshot()
        {
            return Version == 0
                ? null
                : new FplPriceSnapshot(Version, new Dictionary<int, int>(Prices));
        }

        public long Version { get; private set; }

        public FplPriceChangeBatch? GetLatestChanges() => LatestChanges;

        public void SaveSnapshot(IReadOnlyDictionary<int, int> prices)
        {
            Prices.Clear();
            foreach (var price in prices)
            {
                Prices.Add(price.Key, price.Value);
            }

            Version++;
        }

        public void SaveSnapshot(FplPriceChangeCheck priceCheck)
        {
            SaveSnapshot(priceCheck.CurrentPrices);
            if (priceCheck.Changes.Count > 0)
            {
                LatestChanges = new FplPriceChangeBatch(
                    priceCheck.CheckedAtUtc,
                    priceCheck.CurrentEventId,
                    priceCheck.Changes);
            }
        }
    }

    private sealed class ReadyDiscordConnection : IDiscordConnectionReadiness
    {
        public bool IsReady => true;

        public Task WaitUntilReadyAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestNotificationPublisher : IDiscordNotificationPublisher
    {
        public List<Publication> Publications { get; } = [];

        public bool SkipPublication { get; set; }

        public Task<bool> PublishOnceAsync(
            NotificationTargetOptions target,
            string sourceIdentifier,
            string notificationType,
            string message,
            CancellationToken cancellationToken)
        {
            if (SkipPublication)
            {
                return Task.FromResult(false);
            }

            Publications.Add(new Publication(notificationType, message));
            return Task.FromResult(true);
        }
    }

    private sealed record Publication(string NotificationType, string Message);

    public class JobExecutionContextProxy : DispatchProxy
    {
        public required IJobDetail JobDetail { get; set; }

        public required string FireInstanceId { get; set; }

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
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
