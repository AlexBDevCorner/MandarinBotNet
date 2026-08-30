using System.Reflection;
using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Live;
using DiscordBot.Jobs;
using DiscordBot.Notifications;
using DiscordBot.Responses;
using NUnit.Framework;
using Quartz;

namespace DiscordBot.Tests.Jobs;

[TestFixture]
public sealed class FplLiveInsightsJobTests
{
    [Test]
    public async Task Execute_AvailableBaseline_PublishesNothing()
    {
        // Arrange
        var fixture = new JobFixture();

        // Act
        await fixture.Job.Execute(CreateContext());

        // Assert
        fixture.Publisher.Publications.Should().BeEmpty();
    }

    [Test]
    public async Task Execute_OrdinaryPointChange_PublishesNothing()
    {
        // Arrange
        var fixture = new JobFixture();
        await fixture.Job.Execute(CreateContext());
        fixture.Client.ViceCaptainPoints++;

        // Act
        await fixture.Job.Execute(CreateContext());

        // Assert
        fixture.Publisher.Publications.Should().BeEmpty();
    }

    [Test]
    public async Task Execute_DigestDue_PublishesOneConciseMessagePerTarget()
    {
        // Arrange
        var fixture = new JobFixture(targetCount: 2)
        {
            Client = { BenchPlayerPoints = 7 }
        };
        await fixture.Job.Execute(CreateContext());
        fixture.Client.BenchPlayerPoints = 8;

        // Act
        await fixture.Job.Execute(CreateContext());

        // Assert
        fixture.Publisher.Publications.Should().HaveCount(2);
        fixture.Publisher.Publications.Should().OnlyContain(publication =>
            publication.NotificationType == NotificationTypes.FplLiveInsights);
        fixture.Publisher.Publications.Should().OnlyContain(publication =>
            publication.Message.Contains("Полная картина: /live", StringComparison.Ordinal));
        fixture.Publisher.Publications.Should().OnlyContain(publication =>
            !publication.Message.Contains("Лайв-таблица", StringComparison.Ordinal));
    }

    [Test]
    public async Task Execute_CooldownActive_PublishesNothing()
    {
        // Arrange
        var fixture = new JobFixture
        {
            Client =
            {
                BenchPlayerPoints = 7,
                CaptainPoints = 9
            }
        };
        await fixture.Job.Execute(CreateContext());
        fixture.Client.BenchPlayerPoints = 8;
        await fixture.Job.Execute(CreateContext());
        fixture.Publisher.Publications.Clear();
        fixture.TimeProvider.UtcNow = fixture.TimeProvider.UtcNow.AddMinutes(15);
        fixture.Client.CaptainPoints = 10;

        // Act
        await fixture.Job.Execute(CreateContext());

        // Assert
        fixture.Publisher.Publications.Should().BeEmpty();
        fixture.StateStore.State!.Targets[0].PendingHighlights.Should().ContainSingle()
            .Which.Should().BeOfType<CaptainSuccessHighlight>();
    }

    [Test]
    public async Task Execute_FailedTarget_DoesNotAcknowledgePendingDigest()
    {
        // Arrange
        var fixture = new JobFixture
        {
            Client = { BenchPlayerPoints = 7 }
        };
        await fixture.Job.Execute(CreateContext());
        fixture.Client.BenchPlayerPoints = 8;
        fixture.Publisher.FailGuildId = 10;

        // Act
        await fixture.Job.Execute(CreateContext());
        fixture.Publisher.FailGuildId = null;
        await fixture.Job.Execute(CreateContext());

        // Assert
        fixture.Publisher.Publications.Should().ContainSingle();
        fixture.StateStore.State!.Targets[0].PendingHighlights.Should().BeEmpty();
        fixture.StateStore.State.Targets[0].NextDigestSequence.Should().Be(2);
    }

    [Test]
    public async Task Execute_TargetRemovedAndReadded_UsesNextSequenceAndDelivers()
    {
        // Arrange
        var fixture = new JobFixture
        {
            Client = { BenchPlayerPoints = 7 }
        };
        await fixture.Job.Execute(CreateContext());
        fixture.Client.BenchPlayerPoints = 8;
        await fixture.Job.Execute(CreateContext());
        fixture.NotificationOptions.Targets.Clear();
        fixture.TimeProvider.UtcNow = fixture.TimeProvider.UtcNow.AddMinutes(15);
        fixture.Client.BenchPlayerPoints = 9;
        await fixture.Job.Execute(CreateContext());
        fixture.NotificationOptions.Targets.Add(new NotificationTargetOptions
        {
            GuildId = 10,
            ChannelId = 100
        });
        fixture.TimeProvider.UtcNow = fixture.TimeProvider.UtcNow.AddMinutes(45);
        fixture.Client.CaptainPoints = 10;

        // Act
        await fixture.Job.Execute(CreateContext());

        // Assert
        fixture.Publisher.Publications.Should().HaveCount(2);
        fixture.Publisher.Publications.Select(publication => publication.SourceIdentifier)
            .Should().Equal(
                "2026-27-event-3-live-digest-0001",
                "2026-27-event-3-live-digest-0002");
    }

    [Test]
    public async Task Execute_NoActiveGameweek_PublishesNothing()
    {
        // Arrange
        var fixture = new JobFixture
        {
            Client = { HasActiveGameweek = false }
        };

        // Act
        await fixture.Job.Execute(CreateContext());

        // Assert
        fixture.Publisher.Publications.Should().BeEmpty();
        fixture.StateStore.State.Should().BeNull();
    }

    [Test]
    public async Task Execute_SourceUnavailable_PublishesNothing()
    {
        // Arrange
        var fixture = new JobFixture
        {
            Client = { FailBootstrap = true }
        };

        // Act
        await fixture.Job.Execute(CreateContext());

        // Assert
        fixture.Publisher.Publications.Should().BeEmpty();
        fixture.StateStore.State.Should().BeNull();
    }

    private static IJobExecutionContext CreateContext()
    {
        var context = DispatchProxy.Create<
            IJobExecutionContext,
            JobExecutionContextProxy>();
        var proxy = (JobExecutionContextProxy)(object)context;
        proxy.JobDetail = JobBuilder
            .Create<FplLiveInsightsJob>()
            .WithIdentity("fpl-live-insights", "jobs")
            .Build();
        proxy.FireInstanceId = "fire-123";
        return context;
    }

    private sealed class JobFixture
    {
        public JobFixture(int targetCount = 1)
        {
            Options = new FantasyPremierLeagueOptions
            {
                ClassicLeagueId = 123,
                LiveNotificationCooldownMinutes = 60
            };
            Client = new TestFantasyPremierLeagueClient();
            StateStore = new InMemoryStateStore();
            CheckpointStore = new InMemoryCheckpointStore();
            Publisher = new TestNotificationPublisher(CheckpointStore);
            TimeProvider = new MutableTimeProvider(
                new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero));
            var targets = Enumerable.Range(1, targetCount)
                .Select(index => new NotificationTargetOptions
                {
                    GuildId = (ulong)(index * 10),
                    ChannelId = (ulong)(index * 100)
                })
                .ToList();
            NotificationOptions = new NotificationOptions { Targets = targets };
            var detector = new FplLiveHighlightDetectionService(Options);
            var notificationService = new FplLiveNotificationService(
                Options,
                detector,
                StateStore,
                TimeProvider);
            Job = new FplLiveInsightsJob(
                new ReadyDiscordConnection(),
                new FplLiveInsightsService(
                    Client,
                    Options,
                    new FplLiveInsightsCalculationService(Options),
                    TimeProvider,
                    new RecordingLogger<FplLiveInsightsService>()),
                notificationService,
                new FplLiveHighlightMessageComposer(),
                Publisher,
                CheckpointStore,
                NotificationOptions,
                TimeProvider,
                new RecordingLogger<FplLiveInsightsJob>());
        }

        public FantasyPremierLeagueOptions Options { get; }

        public TestFantasyPremierLeagueClient Client { get; set; }

        public InMemoryStateStore StateStore { get; }

        public InMemoryCheckpointStore CheckpointStore { get; }

        public TestNotificationPublisher Publisher { get; }

        public NotificationOptions NotificationOptions { get; }

        public MutableTimeProvider TimeProvider { get; }

        public FplLiveInsightsJob Job { get; }
    }

    private sealed class TestFantasyPremierLeagueClient : IFantasyPremierLeagueClient
    {
        public int BenchPlayerPoints { get; set; }

        public int CaptainPoints { get; set; }

        public int ViceCaptainPoints { get; set; }

        public bool HasActiveGameweek { get; set; } = true;

        public bool FailBootstrap { get; set; }

        public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
            CancellationToken cancellationToken)
        {
            if (FailBootstrap)
            {
                throw new InvalidOperationException("Simulated source failure.");
            }

            return Task.FromResult(new BootstrapStaticResponse
            {
                Events =
                [
                    new PremierLeagueEvent
                    {
                        Id = 3,
                        IsCurrent = HasActiveGameweek,
                        DeadlineTimeEpoch = 1_787_333_400
                    }
                ],
                Elements =
                [
                    new PremierLeagueElement
                    {
                        Id = 1,
                        TeamId = 1,
                        ElementType = 2,
                        WebName = "Captain"
                    },
                    new PremierLeagueElement
                    {
                        Id = 2,
                        TeamId = 2,
                        ElementType = 3,
                        WebName = "Vice"
                    },
                    new PremierLeagueElement
                    {
                        Id = 3,
                        TeamId = 3,
                        ElementType = 4,
                        WebName = "Bench"
                    }
                ]
            });
        }

        public Task<ClassicStandingsResponse> GetClassicStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ClassicStandingsResponse
            {
                LastUpdatedData = new DateTimeOffset(
                    2026,
                    8,
                    30,
                    11,
                    55,
                    0,
                    TimeSpan.Zero),
                Standings = new ClassicStandings
                {
                    Results =
                    [
                        new ClassicStanding
                        {
                            Entry = 1,
                            EntryName = "Alpha",
                            PlayerName = "Alice",
                            Rank = 1
                        }
                    ]
                }
            });
        }

        public Task<EntryEventPicksResponse> GetEntryEventPicksAsync(
            int entryId,
            int eventId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new EntryEventPicksResponse
            {
                EntryHistory = new EntryEventHistory(),
                Picks =
                [
                    new EntryEventPick
                    {
                        Element = 1,
                        Position = 1,
                        Multiplier = 2,
                        IsCaptain = true
                    },
                    new EntryEventPick
                    {
                        Element = 2,
                        Position = 2,
                        Multiplier = 1,
                        IsViceCaptain = true
                    },
                    new EntryEventPick
                    {
                        Element = 3,
                        Position = 12,
                        Multiplier = 0
                    }
                ]
            });
        }

        public Task<EventLiveResponse> GetEventLiveAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new EventLiveResponse
            {
                Elements =
                [
                    new EventLiveElement
                    {
                        Id = 1,
                        Stats = new EventLiveElementStats
                        {
                            TotalPoints = CaptainPoints,
                            Minutes = 90
                        }
                    },
                    new EventLiveElement
                    {
                        Id = 2,
                        Stats = new EventLiveElementStats
                        {
                            TotalPoints = ViceCaptainPoints,
                            Minutes = 90
                        }
                    },
                    new EventLiveElement
                    {
                        Id = 3,
                        Stats = new EventLiveElementStats
                        {
                            TotalPoints = BenchPlayerPoints,
                            Minutes = 90
                        }
                    }
                ]
            });
        }

        public Task<IReadOnlyList<PremierLeagueFixture>> GetFixturesAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PremierLeagueFixture>>(
            [
                CreateFixture(1, 1, eventId),
                CreateFixture(2, 2, eventId),
                CreateFixture(3, 3, eventId)
            ]);
        }

        public Task<HeadToHeadStandingsResponse> GetHeadToHeadStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        private static PremierLeagueFixture CreateFixture(
            int fixtureId,
            int teamId,
            int eventId)
        {
            return new PremierLeagueFixture
            {
                Id = fixtureId,
                EventId = eventId,
                HomeTeamId = teamId,
                AwayTeamId = teamId + 100,
                Started = true,
                Finished = true
            };
        }
    }

    private sealed class InMemoryStateStore : IFplLiveNotificationStateStore
    {
        public FplLiveNotificationState? State { get; private set; }

        public FplLiveNotificationState? Get(int leagueId, string season, int eventId)
        {
            return State is not null &&
                State.ClassicLeagueId == leagueId &&
                State.Season == season &&
                State.EventId == eventId
                    ? State
                    : null;
        }

        public void Save(FplLiveNotificationState state)
        {
            State = state;
        }
    }

    private sealed class InMemoryCheckpointStore : INotificationCheckpointStore
    {
        private readonly HashSet<NotificationCheckpoint> _checkpoints = [];

        public bool IsDelivered(NotificationCheckpoint checkpoint)
        {
            return _checkpoints.Contains(checkpoint);
        }

        public void RecordDelivered(
            NotificationCheckpoint checkpoint,
            DateTimeOffset deliveredAtUtc)
        {
            _checkpoints.Add(checkpoint);
        }
    }

    private sealed class TestNotificationPublisher(
        InMemoryCheckpointStore checkpointStore) : IDiscordNotificationPublisher
    {
        public List<Publication> Publications { get; } = [];

        public ulong? FailGuildId { get; set; }

        public Task<bool> PublishOnceAsync(
            NotificationTargetOptions target,
            string sourceIdentifier,
            string notificationType,
            string message,
            CancellationToken cancellationToken)
        {
            if (target.GuildId == FailGuildId)
            {
                throw new InvalidOperationException("Simulated Discord failure.");
            }

            var checkpoint = new NotificationCheckpoint(
                target.GuildId,
                target.ChannelId,
                sourceIdentifier,
                notificationType);
            if (checkpointStore.IsDelivered(checkpoint))
            {
                return Task.FromResult(false);
            }

            Publications.Add(new Publication(
                target.GuildId,
                sourceIdentifier,
                notificationType,
                message));
            checkpointStore.RecordDelivered(checkpoint, DateTimeOffset.UtcNow);
            return Task.FromResult(true);
        }
    }

    private sealed record Publication(
        ulong GuildId,
        string SourceIdentifier,
        string NotificationType,
        string Message);

    private sealed class ReadyDiscordConnection : IDiscordConnectionReadiness
    {
        public bool IsReady => true;

        public Task WaitUntilReadyAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

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
