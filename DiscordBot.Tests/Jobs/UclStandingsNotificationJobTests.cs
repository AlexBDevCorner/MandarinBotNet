using System.Reflection;
using AwesomeAssertions;
using DiscordBot.Commands;
using DiscordBot.Jobs;
using DiscordBot.Notifications;
using DiscordBot.Tests;
using DiscordBot.Tests.PremierLeague;
using DiscordBot.UclFantasy;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quartz;

namespace DiscordBot.Tests.Jobs;

[TestFixture]
public sealed class UclStandingsNotificationJobTests
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
    public async Task Execute_FirstValidFetch_StoresSilentBaselineWithoutPost()
    {
        // Arrange: A (first valid fetch) -> no Discord post, baseline stored.
        var fixture = CreateFixture();
        var context = CreateContext();

        // Act
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().BeEmpty();
        var state = fixture.CheckpointStore.GetPublicationState(10, 100, 25247707);
        state.Should().NotBeNull();
    }

    [Test]
    public async Task Execute_SameSnapshotWithReorderedMembers_RemainsSilent()
    {
        // Arrange: A again (including response order change) -> no post.
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);
        fixture.Channels[100].SentMessages.Should().BeEmpty();

        // Act
        fixture.Client.Standings = SnapshotA() with
        {
            Members = [.. SnapshotA().Members.Reverse()]
        };
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().BeEmpty();
    }

    [Test]
    public async Task Execute_NonPodiumChange_SendsOnePostPerTargetWithChangedMemberVisible()
    {
        // Arrange: B (change outside top three) -> one post per target.
        var fixture = CreateFixture(targetCount: 2);
        var context = CreateContext();
        await fixture.Job.Execute(context);

        // Act
        var changed = SnapshotA();
        var members = changed.Members.ToList();
        members[3] = members[3] with { TotalPoints = members[3].TotalPoints + 25, MatchdayPoints = 95 };
        fixture.Client.Standings = changed with { Members = members };
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().ContainSingle();
        fixture.Channels[200].SentMessages.Should().ContainSingle();
        fixture.Channels[100].SentMessages[0].Should().Contain("Delta Rovers");
        fixture.Channels[200].SentMessages[0].Should().Contain("Delta Rovers");
    }

    [Test]
    public async Task Execute_RepeatAfterChangeAndRestart_RemainsSilent()
    {
        // Arrange: B again and B after restart -> no post.
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);
        var changed = SnapshotA();
        var members = changed.Members.ToList();
        members[0] = members[0] with { TotalPoints = members[0].TotalPoints + 1 };
        fixture.Client.Standings = changed with { Members = members };
        await fixture.Job.Execute(context);
        fixture.Channels[100].SentMessages.Should().ContainSingle();

        // Act
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().ContainSingle();

        // Act: restart preserving SQLite.
        SqliteConnection.ClearAllPools();
        var restarted = CreateFixture(existingDatabasePath: _databasePath);
        restarted.Client.Standings = fixture.Client.Standings;
        await restarted.Job.Execute(context);

        // Assert
        restarted.Channels[100].SentMessages.Should().BeEmpty();
    }

    [Test]
    public async Task Execute_ReturnToPreviousTable_SendsNewPost()
    {
        // Arrange: A -> B -> A is two genuine changes.
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);
        var snapshotA = SnapshotA();

        var changed = SnapshotA();
        var members = changed.Members.ToList();
        members[1] = members[1] with { TotalPoints = members[1].TotalPoints + 5 };
        fixture.Client.Standings = changed with { Members = members };
        await fixture.Job.Execute(context);
        fixture.Channels[100].SentMessages.Should().ContainSingle();

        // Act
        fixture.Client.Standings = snapshotA;
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().HaveCount(2);
    }

    [Test]
    public async Task Execute_OneTargetFails_RetriesOnlyFailedTarget()
    {
        // Arrange
        var fixture = CreateFixture(targetCount: 2);
        var context = CreateContext();
        await fixture.Job.Execute(context);

        var changed = SnapshotA();
        var members = changed.Members.ToList();
        members[0] = members[0] with { TotalPoints = members[0].TotalPoints + 3 };
        fixture.Client.Standings = changed with { Members = members };
        fixture.Channels[200].FailSend = true;
        await fixture.Job.Execute(context);
        fixture.Channels[100].SentMessages.Should().ContainSingle();
        fixture.Channels[200].SentMessages.Should().BeEmpty();

        // Act
        fixture.Channels[200].FailSend = false;
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().ContainSingle();
        fixture.Channels[200].SentMessages.Should().ContainSingle();
    }

    [Test]
    public async Task Execute_BadResponseOrMissingPage_PreservesBaselineWithoutPost()
    {
        // Arrange
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);
        var baseline = fixture.CheckpointStore.GetPublicationState(10, 100, 25247707);
        baseline.Should().NotBeNull();

        // Act: transient failure must not replace the good baseline.
        fixture.Client.ThrowApiException = true;
        await fixture.Job.Execute(context);
        fixture.Client.ThrowApiException = false;
        fixture.Channels[100].SentMessages.Should().BeEmpty();

        // Act: empty standings must not replace the baseline either.
        fixture.Client.Standings = SnapshotA() with { Members = [] };
        await fixture.Job.Execute(context);
        fixture.Channels[100].SentMessages.Should().BeEmpty();

        // Assert: a later genuine change still delivers once.
        var changed = SnapshotA();
        var members = changed.Members.ToList();
        members[2] = members[2] with { TotalPoints = members[2].TotalPoints + 7 };
        fixture.Client.Standings = changed with { Members = members };
        await fixture.Job.Execute(context);
        fixture.Channels[100].SentMessages.Should().ContainSingle();
    }

    [Test]
    public async Task Execute_UnchangedResult_LogsSkippedWithoutDiscordSpam()
    {
        // Arrange
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);
        var changed = SnapshotA();
        var members = changed.Members.ToList();
        members[0] = members[0] with { TotalPoints = members[0].TotalPoints + 1 };
        fixture.Client.Standings = changed with { Members = members };
        await fixture.Job.Execute(context);
        fixture.Channels[100].SentMessages.Should().ContainSingle();
        var initialLogCount = fixture.JobLogger.Entries.Count;
        fixture.Channels[100].SentMessages.Clear();

        // Act
        await fixture.Job.Execute(context);

        // Assert
        fixture.Channels[100].SentMessages.Should().BeEmpty();
        fixture.JobLogger.Entries.Skip(initialLogCount).Should().Contain(entry =>
            entry.Properties.GetValueOrDefault("Outcome") as string == "SkippedUnchangedStandings");
    }

    [Test]
    public async Task StandingsCommand_AfterScheduledSuppression_StillReturnsSnapshotWithoutTouchingBaseline()
    {
        // Arrange: scheduled job suppresses its second identical run.
        var fixture = CreateFixture();
        var context = CreateContext();
        await fixture.Job.Execute(context);
        await fixture.Job.Execute(context);
        fixture.Channels[100].SentMessages.Should().BeEmpty();
        var baselineBefore = fixture.CheckpointStore.GetPublicationState(10, 100, 25247707);

        var interaction = new CommandTestInteraction();
        var handler = new UclStandingsCommandHandler(
            fixture.Provider,
            new UclFantasyMessageCompositionService(TestTimeZones.Riga()),
            NullLogger<UclStandingsCommandHandler>.Instance);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Contain("Alpha FC");
        var baselineAfter = fixture.CheckpointStore.GetPublicationState(10, 100, 25247707);
        baselineAfter.Should().BeEquivalentTo(baselineBefore);
    }

    internal static UclLeagueStandings SnapshotA()
    {
        return new UclLeagueStandings(
            25247707,
            "Great Messi League",
            1,
            2,
            [
                new UclLeagueStanding(101, "Alpha FC", "Anna", 1, 1, 200, 60),
                new UclLeagueStanding(102, "Beta United", "Boris", 2, 2, 190, 55),
                new UclLeagueStanding(103, "Gamma City", "Gita", 3, 3, 180, 40),
                new UclLeagueStanding(104, "Delta Rovers", "Dana", 4, 4, 170, 35)
            ]);
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
            .Create<UclStandingsNotificationJob>()
            .WithIdentity("ucl-standings", "jobs")
            .Build();
        proxy.FireInstanceId = "fire-123";
        return context;
    }

    private sealed class JobFixture
    {
        public JobFixture(string databasePath, int targetCount, string? existingDatabasePath)
        {
            var effectivePath = existingDatabasePath ?? databasePath;
            Client = new TestUclClient();
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
            JobLogger = new RecordingLogger<UclStandingsNotificationJob>();
            var leagueOptions = new UclFantasyOptions { LeagueId = 25247707 };
            var timeZone = new ConfiguredTimeZone(new JobSchedulesOptions { TimeZoneId = "Europe/Riga" });
            Provider = new UclLeagueStandingsProvider(
                Client,
                leagueOptions,
                NullLogger<UclLeagueStandingsProvider>.Instance);
            Job = new UclStandingsNotificationJob(
                new ReadyDiscordConnection(),
                Provider,
                Publisher,
                CheckpointStore,
                CheckpointStore,
                leagueOptions,
                NotificationOptions,
                new UclStandingsSnapshotService(),
                new UclFantasyMessageCompositionService(timeZone),
                TimeProvider,
                JobLogger);
        }

        public TestUclClient Client { get; }

        public UclLeagueStandingsProvider Provider { get; }

        public SqliteNotificationCheckpointStore CheckpointStore { get; }

        public DiscordNotificationPublisher Publisher { get; }

        public Dictionary<ulong, FakeNotificationChannel> Channels { get; }

        public NotificationOptions NotificationOptions { get; }

        public MutableTimeProvider TimeProvider { get; }

        public RecordingLogger<UclStandingsNotificationJob> JobLogger { get; }

        public UclStandingsNotificationJob Job { get; }
    }

    private sealed class TestUclClient : IUclFantasyClient
    {
        public UclLeagueStandings Standings { get; set; } = SnapshotA();

        public bool ThrowApiException { get; set; }

        public Task<UclFantasyWebConfigurationResponse> GetWebConfigurationAsync(
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<UclFantasyFixturesResponse> GetFixturesAsync(
            UclFantasyWebConfigurationValue webConfiguration,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<UclLeagueStandings> GetLeagueStandingsAsync(
            UclLeagueStandingsQuery query,
            CancellationToken cancellationToken)
        {
            if (ThrowApiException)
            {
                throw new UclFantasyApiException(
                    UclFantasyFailureKind.Transient,
                    "Transient test failure.");
            }

            return Task.FromResult(Standings);
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

            allowEveryoneMention.Should().BeFalse();
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

    private sealed class CommandTestInteraction : IDiscordSlashCommandInteraction
    {
        public string Name => DiscordApplicationCommands.UclStandingsName;

        public string UserMention => "<@123>";

        public List<string> Messages { get; } = [];

        public string? GetStringOption(string name) => null;

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
