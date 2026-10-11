using System.Reflection;
using AwesomeAssertions;
using DiscordBot.Commands;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Standings;
using DiscordBot.Jobs;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;
using DiscordBot.Responses;
using DiscordBot.Tests;
using DiscordBot.Tests.PremierLeague;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Quartz;

namespace DiscordBot.Tests.Jobs;

[TestFixture]
public sealed class PremierLeagueClassicStandingsInformationJobTests
{
    private string _testDirectory = null!;
    private string _databasePath = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "MandarinBotNet.Tests",
            Guid.NewGuid().ToString("N"));
        _databasePath = Path.Combine(_testDirectory, "notification-state.db");
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Execute_FourIdenticalSnapshotsWithDifferentTimestamps_PublishesOnlyOnce()
    {
        // Arrange: 5-8 October style snapshots, same podium and 74-point
        // winner, different API timestamps.
        var fixture = CreateFixture();
        var context = CreateContext();

        // Act
        await fixture.Job.Execute(context);
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().ContainSingle();
        var message = fixture.Channels[100].SentMessages[0];
        message.Should().Contain("Antony The Beaver");
        message.Should().Contain("319");
        message.Should().Contain("74");
    }

    [Test]
    public async Task Execute_UnchangedAfterRestart_RemainsSilent()
    {
        // Arrange
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);
        fixture.Channels[100].SentMessages.Should().ContainSingle();

        // Act: simulate a process restart that preserves the SQLite volume.
        SqliteConnection.ClearAllPools();
        var restarted = CreateFixture(existingDatabasePath: _databasePath);
        restarted.Client.Standings = OctSnapshot();
        restarted.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await restarted.Job.Execute(context);

        // Assert
        restarted.Channels[100].SentMessages.Should().BeEmpty();
    }

    [Test]
    public async Task Execute_GenuinePointChange_PublishesAgain()
    {
        // Arrange
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);

        // Act
        fixture.Client.Standings[1].Total++;
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().HaveCount(2);
    }

    [Test]
    public async Task Execute_RankChange_PublishesAgain()
    {
        // Arrange
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);

        // Act
        fixture.Client.Standings[0].Rank = 2;
        fixture.Client.Standings[0].LastRank = 1;
        fixture.Client.Standings[1].Rank = 1;
        fixture.Client.Standings[1].LastRank = 2;
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().HaveCount(2);
        fixture.Channels[100].SentMessages[1].Should().Contain("FC Laimdots");
    }

    [Test]
    public async Task Execute_EntrantChange_PublishesAgain()
    {
        // Arrange
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);

        // Act
        fixture.Client.Standings.Add(new ClassicStanding
        {
            Id = 4,
            Entry = 44,
            EntryName = "Newcomer",
            PlayerName = "Nina",
            Rank = 4,
            LastRank = 4,
            Total = 100,
            EventTotal = 40
        });
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().HaveCount(2);
    }

    [Test]
    public async Task Execute_WinnerChange_PublishesAgain()
    {
        // Arrange
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);

        // Act
        fixture.Client.Standings[1].EventTotal = 80;
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().HaveCount(2);
        fixture.Channels[100].SentMessages[1].Should().Contain("FC Laimdots");
    }

    [Test]
    public async Task Execute_TiedWinners_PublishesAgain()
    {
        // Arrange
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);

        // Act
        fixture.Client.Standings[1].EventTotal = 74;
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().HaveCount(2);
        fixture.Channels[100].SentMessages[1].Should().Contain("разделили команды");
    }

    [Test]
    public async Task Execute_ReorderedInput_DoesNotPublishAgain()
    {
        // Arrange
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);

        // Act: same semantic table, different enumeration order.
        fixture.Client.Standings.Reverse();
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().ContainSingle();
    }

    [Test]
    public async Task Execute_ReturnToPreviousTable_PublishesAgain()
    {
        // Arrange: A -> B -> A must be two changes after the initial A.
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);
        var snapshotA = OctSnapshot();

        fixture.Client.Standings[0].Total++;
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);

        // Act: return to A with a fresh timestamp.
        fixture.Client.Standings = snapshotA;
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().HaveCount(3);
    }

    [Test]
    public async Task Execute_MultipleTargets_PublishesOncePerTarget()
    {
        // Arrange
        var fixture = CreateFixture(targetCount: 2);
        var context = CreateContext();

        // Act
        await fixture.Job.Execute(context);
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);
        fixture.Client.Standings[0].Total++;
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().HaveCount(2);
        fixture.Channels[200].SentMessages.Should().HaveCount(2);
    }

    [Test]
    public async Task Execute_OneTargetFails_RetriesOnlyFailedTarget()
    {
        // Arrange
        var fixture = CreateFixture(targetCount: 2);
        var context = CreateContext();
        fixture.Channels[200].FailSend = true;

        await fixture.Job.Execute(context);

        // Act: the failed target retries with the same snapshot.
        fixture.Channels[200].FailSend = false;
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);

        // Assert: the successful target is not duplicated.
        fixture.Channels[100].SentMessages.Should().ContainSingle();
        fixture.Channels[200].SentMessages.Should().ContainSingle();
    }

    [Test]
    public async Task Execute_FailedDelivery_DoesNotCommitBaseline()
    {
        // Arrange
        var fixture = CreateFixture();
        var context = CreateContext();
        fixture.Channels[100].FailSend = true;

        await fixture.Job.Execute(context);
        fixture.Channels[100].SentMessages.Should().BeEmpty();

        // Act: retry after the failure still delivers the same update.
        fixture.Channels[100].FailSend = false;
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().ContainSingle();
    }

    [Test]
    public async Task Execute_EmptyResponse_DoesNotOverwriteBaseline()
    {
        // Arrange
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);

        // Act: a transient empty result must not replace the good baseline.
        fixture.Client.Standings = [];
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);
        fixture.Client.Standings = OctSnapshot();
        fixture.Client.Standings[0].Total++;
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().HaveCount(2);
    }

    [Test]
    public async Task Execute_UnchangedResult_IsObservableWithoutDiscordSpam()
    {
        // Arrange
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);
        var initialLogCount = fixture.JobLogger.Entries.Count;
        fixture.Channels[100].SentMessages.Clear();
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);

        // Act
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().BeEmpty();
        fixture.JobLogger.Entries.Skip(initialLogCount).Should().Contain(entry =>
            entry.Properties.GetValueOrDefault("Outcome") as string == "SkippedUnchangedStandings");
    }

    [Test]
    public async Task Execute_RealUpdate_PreservesRussianFormatting()
    {
        // Arrange
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);

        // Act
        fixture.Client.Standings[0].Total++;
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().HaveCount(2);
        var message = fixture.Channels[100].SentMessages[1];
        message.Should().StartWith("🏆 Лига Пельменных Обнимашек:");
        message.Should().Contain("Antony The Beaver");
    }

    [Test]
    public async Task StandingsCommand_AfterScheduledSuppression_StillReturnsSnapshot()
    {
        // Arrange: the scheduled job suppresses its second identical run.
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);
        fixture.Client.LastUpdatedData = fixture.Client.LastUpdatedData.AddDays(1);
        await fixture.Job.Execute(context);
        fixture.Channels[100].SentMessages.Should().ContainSingle();

        var commandClient = new CommandTestClient(fixture.Client.Standings);
        var interaction = new CommandTestInteraction();
        var handler = new StandingsCommandHandler(
            commandClient,
            new FantasyPremierLeagueOptions { ClassicLeagueId = 123, HeadToHeadLeagueId = 456 },
            new PremierLeagueMessageCompositionService(TestTimeZones.Riga()),
            new FplStandingsSelectionService(),
            new RecordingLogger<StandingsCommandHandler>());

        // Act: on-demand /standings must return current information anyway.
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Contain("Antony The Beaver");
    }

    private JobFixture CreateFixture(int targetCount = 1, string? existingDatabasePath = null)
    {
        return new JobFixture(_databasePath, targetCount, existingDatabasePath);
    }

    private static IJobExecutionContext CreateContext()
    {
        var context = DispatchProxy.Create<
            IJobExecutionContext,
            JobExecutionContextProxy>();
        var proxy = (JobExecutionContextProxy)(object)context;
        proxy.JobDetail = JobBuilder
            .Create<PremierLeagueClassicStandingsInformationJob>()
            .WithIdentity("classic-standings", "jobs")
            .Build();
        proxy.FireInstanceId = "fire-123";
        return context;
    }

    internal static List<ClassicStanding> OctSnapshot()
    {
        return
        [
            new ClassicStanding
            {
                Id = 1,
                Entry = 11,
                EntryName = "Antony The Beaver",
                PlayerName = "Antony",
                Rank = 1,
                LastRank = 1,
                Total = 319,
                EventTotal = 74
            },
            new ClassicStanding
            {
                Id = 2,
                Entry = 22,
                EntryName = "FC Laimdots",
                PlayerName = "Laima",
                Rank = 2,
                LastRank = 2,
                Total = 295,
                EventTotal = 60
            },
            new ClassicStanding
            {
                Id = 3,
                Entry = 33,
                EntryName = "FC Jam",
                PlayerName = "Jam",
                Rank = 3,
                LastRank = 3,
                Total = 249,
                EventTotal = 55
            }
        ];
    }

    private sealed class JobFixture
    {
        public JobFixture(string databasePath, int targetCount, string? existingDatabasePath)
        {
            var effectivePath = existingDatabasePath ?? databasePath;
            Client = new TestFantasyPremierLeagueClient();
            CheckpointStore = new SqliteNotificationCheckpointStore(effectivePath);
            TimeProvider = new MutableTimeProvider(
                new DateTimeOffset(2026, 10, 8, 17, 0, 0, TimeSpan.Zero));
            var targets = Enumerable.Range(1, targetCount)
                .Select(index => new NotificationTargetOptions
                {
                    GuildId = 10,
                    ChannelId = (ulong)(index * 100)
                })
                .ToList();
            NotificationOptions = new NotificationOptions { Targets = targets };
            Channels = targets.ToDictionary(
                target => target.ChannelId,
                _ => new FakeNotificationChannel());
            var resolver = new FakeChannelResolver(Channels);
            var coordinator = new NotificationDeliveryCoordinator(
                CheckpointStore,
                TimeProviderSystem.Instance,
                new RecordingLogger<NotificationDeliveryCoordinator>());
            Publisher = new DiscordNotificationPublisher(
                resolver,
                coordinator,
                new RecordingLogger<DiscordNotificationPublisher>());
            JobLogger = new RecordingLogger<PremierLeagueClassicStandingsInformationJob>();
            var leagueOptions = new FantasyPremierLeagueOptions { ClassicLeagueId = 123 };
            var timeZone = new ConfiguredTimeZone(new JobSchedulesOptions { TimeZoneId = "Europe/Riga" });
            Job = new PremierLeagueClassicStandingsInformationJob(
                new ReadyDiscordConnection(),
                Client,
                Publisher,
                CheckpointStore,
                CheckpointStore,
                leagueOptions,
                NotificationOptions,
                new StandingsPublicationEligibilityService(timeZone),
                new ClassicStandingsSnapshotService(),
                new StandingsChangeService(),
                new WinnerSelectionService(),
                new PremierLeagueMessageCompositionService(timeZone),
                TimeProvider,
                JobLogger);
        }

        public TestFantasyPremierLeagueClient Client { get; }

        public SqliteNotificationCheckpointStore CheckpointStore { get; }

        public DiscordNotificationPublisher Publisher { get; }

        public Dictionary<ulong, FakeNotificationChannel> Channels { get; }

        public NotificationOptions NotificationOptions { get; }

        public MutableTimeProvider TimeProvider { get; }

        public RecordingLogger<PremierLeagueClassicStandingsInformationJob> JobLogger { get; }

        public PremierLeagueClassicStandingsInformationJob Job { get; }
    }

    private sealed class TestFantasyPremierLeagueClient : IFantasyPremierLeagueClient
    {
        public List<ClassicStanding> Standings { get; set; } = OctSnapshot();

        public DateTimeOffset LastUpdatedData { get; set; } = new(
            2026,
            10,
            8,
            16,
            0,
            0,
            TimeSpan.Zero);

        public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ClassicStandingsResponse> GetClassicStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ClassicStandingsResponse
            {
                LastUpdatedData = LastUpdatedData,
                Standings = new ClassicStandings { Results = Standings }
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
            throw new NotSupportedException();
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

    private sealed class FakeChannelResolver(
        Dictionary<ulong, FakeNotificationChannel> channels)
        : IDiscordNotificationChannelResolver
    {
        public DiscordNotificationDestination Resolve(ulong guildId, ulong channelId)
        {
            return channels.TryGetValue(channelId, out var channel)
                ? new DiscordNotificationDestination(true, channel)
                : new DiscordNotificationDestination(true, null);
        }
    }

    private sealed class FakeNotificationChannel : IDiscordNotificationChannel
    {
        public List<string> SentMessages { get; } = [];

        public bool FailSend { get; set; }

        public Task SendMessageAsync(string content, bool allowEveryoneMention)
        {
            if (FailSend)
            {
                throw new InvalidOperationException("Discord rejected the message.");
            }

            SentMessages.Add(content);
            return Task.CompletedTask;
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

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class TimeProviderSystem
    {
        public static TimeProvider Instance { get; } = TimeProvider.System;
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

    private sealed class CommandTestClient(List<ClassicStanding> standings) : IFantasyPremierLeagueClient
    {
        public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ClassicStandingsResponse> GetClassicStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ClassicStandingsResponse
            {
                LastUpdatedData = DateTimeOffset.UtcNow,
                Standings = new ClassicStandings { Results = standings }
            });
        }

        public Task<HeadToHeadStandingsResponse> GetHeadToHeadStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HeadToHeadStandingsResponse
            {
                HeadToHeadStandings = new HeadToHeadStandings { Results = [] }
            });
        }

        public Task<EntryEventPicksResponse> GetEntryEventPicksAsync(
            int entryId,
            int eventId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
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

    private sealed class CommandTestInteraction : IDiscordSlashCommandInteraction
    {
        public string Name => "standings";

        public string UserMention => "<@123>";

        public List<string> Messages { get; } = [];

        public string? GetStringOption(string name) => name == "league" ? "classic" : null;

        public long? GetIntegerOption(string name) => null;

        public Task RespondAsync(string content)
        {
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task DeferAsync() => Task.CompletedTask;

        public Task ModifyOriginalResponseAsync(string content)
        {
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task FollowupAsync(string content)
        {
            Messages.Add(content);
            return Task.CompletedTask;
        }
    }
}
