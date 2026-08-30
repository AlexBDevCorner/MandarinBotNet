using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Live;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Live;

[TestFixture]
public sealed class FplLiveNotificationServiceTests
{
    private string _testDirectory = null!;
    private string _databasePath = null!;
    private FantasyPremierLeagueOptions _options = null!;
    private MutableTimeProvider _timeProvider = null!;
    private NotificationTargetOptions _firstTarget = null!;
    private NotificationTargetOptions _secondTarget = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "MandarinBotNet.Tests",
            Guid.NewGuid().ToString("N"));
        _databasePath = Path.Combine(_testDirectory, "fpl-live.db");
        _options = new FantasyPremierLeagueOptions
        {
            ClassicLeagueId = 123,
            LiveNotificationCooldownMinutes = 60,
            SignificantLiveRankChange = 2,
            AutomaticSubstitutionHighlightPoints = 5
        };
        _timeProvider = new MutableTimeProvider(FplLiveTestData.CapturedAtUtc);
        _firstTarget = new NotificationTargetOptions
        {
            GuildId = 10,
            ChannelId = 100
        };
        _secondTarget = new NotificationTargetOptions
        {
            GuildId = 20,
            ChannelId = 200
        };
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
    public void Observe_FirstObservation_EstablishesBaselineWithoutDigest()
    {
        // Arrange
        var service = CreateService();

        // Act
        var evaluation = service.Observe(
            FplLiveTestData.CreateGameweek(),
            [_firstTarget]);

        // Assert
        evaluation.Outcome.Should().Be(FplLiveNotificationOutcome.BaselineEstablished);
        evaluation.Digests.Should().BeEmpty();
        new SqliteFplLiveNotificationStateStore(_databasePath)
            .Get(123, "2026/27", 3)
            .Should().NotBeNull();
    }

    [Test]
    public void Observe_FirstMeaningfulChange_IsImmediatelyPublishable()
    {
        // Arrange
        var service = CreateService();
        service.Observe(CreateBenchGameweek(7), [_firstTarget]);

        // Act
        var evaluation = service.Observe(CreateBenchGameweek(8), [_firstTarget]);

        // Assert
        evaluation.Outcome.Should().Be(FplLiveNotificationOutcome.DigestReady);
        evaluation.Digests.Should().ContainSingle();
        evaluation.Digests[0].SourceIdentifier.Should()
            .Be("2026-27-event-3-live-digest-0001");
    }

    [Test]
    public void Observe_AfterSuccessfulPublication_QueuesAndCoalescesUntilExactCooldown()
    {
        // Arrange
        var service = CreateService();
        service.Observe(CreateBenchGameweek(7), [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(15);
        var firstDigest = service.Observe(CreateBenchGameweek(8), [_firstTarget])
            .Digests.Single();
        service.MarkPublished(firstDigest);

        // Act
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(30);
        var queued = service.Observe(
            CreateBenchAndCaptainGameweek(9, 20, 30),
            [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(45);
        var merged = service.Observe(
            CreateBenchAndCaptainGameweek(15, 22, 45),
            [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(75);
        var due = service.Observe(
            CreateBenchAndCaptainGameweek(15, 22, 75),
            [_firstTarget]);

        // Assert
        queued.Outcome.Should().Be(FplLiveNotificationOutcome.CooldownActive);
        merged.Outcome.Should().Be(FplLiveNotificationOutcome.CooldownActive);
        due.Outcome.Should().Be(FplLiveNotificationOutcome.DigestReady);
        due.Digests.Should().ContainSingle();
        due.Digests[0].Highlights.Should().ContainSingle();
        due.Digests[0].Highlights[0].Should().BeOfType<CaptainSuccessHighlight>()
            .Which.EffectivePoints.Should().Be(22);
        due.Digests[0].SourceIdentifier.Should()
            .Be("2026-27-event-3-live-digest-0002");
    }

    [Test]
    public void Observe_BenchGrowsDuringCooldown_KeepsOneCurrentHighlight()
    {
        // Arrange
        var service = CreateService();
        service.Observe(CreateCaptainGameweek(18), [_firstTarget]);
        var firstDigest = service.Observe(CreateCaptainGameweek(20), [_firstTarget])
            .Digests.Single();
        service.MarkPublished(firstDigest);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(15);
        service.Observe(CreateBenchAndCaptainGameweek(8, 20, 15), [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(30);
        service.Observe(CreateBenchAndCaptainGameweek(12, 20, 30), [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(60);

        // Act
        var evaluation = service.Observe(
            CreateBenchAndCaptainGameweek(15, 20, 60),
            [_firstTarget]);

        // Assert
        evaluation.Digests.Should().ContainSingle();
        evaluation.Digests[0].Highlights.Should().ContainSingle();
        evaluation.Digests[0].Highlights[0]
            .Should().BeOfType<BenchThresholdReachedHighlight>()
            .Which.BenchPoints.Should().Be(15);
    }

    [Test]
    public void Observe_DifferentHighlightsDuringCooldown_CombinesOneDigest()
    {
        // Arrange
        var service = CreateService();
        service.Observe(CreateBenchGameweek(7), [_firstTarget]);
        var firstDigest = service.Observe(CreateBenchGameweek(8), [_firstTarget])
            .Digests.Single();
        service.MarkPublished(firstDigest);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(15);
        service.Observe(CreateCaptainGameweek(20), [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(30);
        var withSubstitution = CreateCaptainGameweek(22) with
        {
            CapturedAtUtc = FplLiveTestData.CapturedAtUtc.AddMinutes(30),
            AutomaticSubstitutionSalvations =
            [FplLiveTestData.CreateSubstitution(8)]
        };
        service.Observe(withSubstitution, [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(60);

        // Act
        var evaluation = service.Observe(withSubstitution with
        {
            CapturedAtUtc = FplLiveTestData.CapturedAtUtc.AddMinutes(60)
        }, [_firstTarget]);

        // Assert
        evaluation.Digests.Should().ContainSingle();
        evaluation.Digests[0].Highlights.Should().HaveCount(2);
        evaluation.Digests[0].Highlights.Should().Contain(highlight =>
            highlight is CaptainSuccessHighlight);
        evaluation.Digests[0].Highlights.Should().Contain(highlight =>
            highlight is AutomaticSubstitutionHighlight);
    }

    [Test]
    public void Observe_LeaderChangesTwiceDuringCooldown_KeepsLatestLeader()
    {
        // Arrange
        var service = CreateService();
        service.Observe(CreateCaptainGameweek(18), [_firstTarget]);
        var firstDigest = service.Observe(CreateCaptainGameweek(20), [_firstTarget])
            .Digests.Single();
        service.MarkPublished(firstDigest);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(15);
        service.Observe(CreateLeaderGameweek(2, 15), [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(30);
        service.Observe(CreateLeaderGameweek(3, 30), [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(60);

        // Act
        var evaluation = service.Observe(CreateLeaderGameweek(3, 60), [_firstTarget]);

        // Assert
        evaluation.Digests.Should().ContainSingle();
        evaluation.Digests[0].Highlights.Should().ContainSingle();
        evaluation.Digests[0].Highlights[0].Should().BeOfType<LeaderChangedHighlight>()
            .Which.NewLeaderEntryId.Should().Be(3);
    }

    [Test]
    public void Observe_PendingLeaderBecomesTied_RemovesLeaderHighlight()
    {
        // Arrange
        var service = CreateService();
        service.Observe(CreateCaptainGameweek(18), [_firstTarget]);
        var firstDigest = service.Observe(CreateCaptainGameweek(20), [_firstTarget])
            .Digests.Single();
        service.MarkPublished(firstDigest);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(15);
        service.Observe(
            CreateScoreGameweek(15, 120, 125, 110, 105),
            [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(30);
        service.Observe(
            CreateScoreGameweek(30, 120, 125, 125, 105),
            [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(60);

        // Act
        var evaluation = service.Observe(
            CreateScoreGameweek(60, 120, 125, 125, 105),
            [_firstTarget]);

        // Assert
        evaluation.Outcome.Should().Be(FplLiveNotificationOutcome.NothingInteresting);
        evaluation.Digests.Should().BeEmpty();
    }

    [Test]
    public void Observe_WithoutPublicationAcknowledgement_RetriesPendingDigest()
    {
        // Arrange
        var service = CreateService();
        service.Observe(CreateBenchGameweek(7), [_firstTarget]);
        var firstEvaluation = service.Observe(CreateBenchGameweek(8), [_firstTarget]);

        // Act
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(15);
        var retryEvaluation = service.Observe(CreateBenchGameweek(9), [_firstTarget]);

        // Assert
        retryEvaluation.Outcome.Should().Be(FplLiveNotificationOutcome.DigestReady);
        retryEvaluation.Digests[0].SourceIdentifier.Should()
            .Be(firstEvaluation.Digests[0].SourceIdentifier);
        retryEvaluation.Digests[0].Highlights[0]
            .Should().BeOfType<BenchThresholdReachedHighlight>()
            .Which.BenchPoints.Should().Be(9);
    }

    [Test]
    public void Observe_RecreatedService_RestoresCooldownAndPendingHighlights()
    {
        // Arrange
        var service = CreateService();
        service.Observe(CreateCaptainGameweek(18), [_firstTarget]);
        var digest = service.Observe(CreateCaptainGameweek(20), [_firstTarget])
            .Digests.Single();
        service.MarkPublished(digest);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(15);
        service.Observe(CreateBenchAndCaptainGameweek(8, 20, 15), [_firstTarget]);
        var restartedService = CreateService();

        // Act
        var duringCooldown = restartedService.Observe(
            CreateBenchAndCaptainGameweek(10, 20, 30),
            [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(60);
        var afterCooldown = restartedService.Observe(
            CreateBenchAndCaptainGameweek(12, 20, 60),
            [_firstTarget]);

        // Assert
        duringCooldown.Outcome.Should().Be(FplLiveNotificationOutcome.CooldownActive);
        afterCooldown.Outcome.Should().Be(FplLiveNotificationOutcome.DigestReady);
        afterCooldown.Digests[0].Highlights[0]
            .Should().BeOfType<BenchThresholdReachedHighlight>()
            .Which.BenchPoints.Should().Be(12);
    }

    [Test]
    public void Observe_NewGameweek_EstablishesIndependentBaseline()
    {
        // Arrange
        var service = CreateService();
        service.Observe(CreateBenchGameweek(7), [_firstTarget]);
        service.Observe(CreateBenchGameweek(8), [_firstTarget]);

        // Act
        var evaluation = service.Observe(
            FplLiveTestData.CreateGameweek(eventId: 4),
            [_firstTarget]);

        // Assert
        evaluation.Outcome.Should().Be(FplLiveNotificationOutcome.BaselineEstablished);
        evaluation.Digests.Should().BeEmpty();
        new SqliteFplLiveNotificationStateStore(_databasePath)
            .Get(123, "2026/27", 3)!.Targets[0].PendingHighlights
            .Should().ContainSingle();
    }

    [Test]
    public void MarkPublished_MultipleTargets_UpdatesOnlySuccessfulTarget()
    {
        // Arrange
        var service = CreateService();
        var targets = new[] { _firstTarget, _secondTarget };
        service.Observe(CreateBenchGameweek(7), targets);
        var firstEvaluation = service.Observe(CreateBenchGameweek(8), targets);

        // Act
        service.MarkPublished(firstEvaluation.Digests.Single(digest =>
            digest.GuildId == _firstTarget.GuildId));
        var nextEvaluation = service.Observe(CreateBenchGameweek(9), targets);

        // Assert
        nextEvaluation.Digests.Should().ContainSingle();
        nextEvaluation.Digests[0].GuildId.Should().Be(_secondTarget.GuildId);
        var state = new SqliteFplLiveNotificationStateStore(_databasePath)
            .Get(123, "2026/27", 3)!;
        state.Targets.Single(target => target.GuildId == _firstTarget.GuildId)
            .PendingHighlights.Should().BeEmpty();
        state.Targets.Single(target => target.GuildId == _secondTarget.GuildId)
            .PendingHighlights.Should().ContainSingle();
    }

    [Test]
    public void Observe_TargetRemovedAndReadded_PreservesNextDigestSequence()
    {
        // Arrange
        var service = CreateService();
        service.Observe(CreateBenchGameweek(7), [_firstTarget]);
        var firstDigest = service.Observe(CreateBenchGameweek(8), [_firstTarget])
            .Digests.Single();
        service.MarkPublished(firstDigest);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(15);
        service.Observe(CreateBenchGameweek(9), []);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(60);

        // Act
        var evaluation = service.Observe(CreateCaptainGameweek(20), [_firstTarget]);

        // Assert
        evaluation.Digests.Should().ContainSingle();
        evaluation.Digests[0].Sequence.Should().Be(2);
        evaluation.Digests[0].SourceIdentifier.Should()
            .Be("2026-27-event-3-live-digest-0002");
    }

    [Test]
    public void Observe_RankReturnsToOriginalDuringCooldown_RemovesObsoleteHighlight()
    {
        // Arrange
        var service = CreateService();
        service.Observe(CreateCaptainGameweek(18), [_firstTarget]);
        var digest = service.Observe(CreateCaptainGameweek(20), [_firstTarget])
            .Digests.Single();
        service.MarkPublished(digest);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(15);
        service.Observe(CreateRankGameweek(entryTwoRank: 4, entryFourRank: 2, 15), [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(30);
        service.Observe(CreateRankGameweek(entryTwoRank: 2, entryFourRank: 4, 30), [_firstTarget]);
        _timeProvider.UtcNow = FplLiveTestData.CapturedAtUtc.AddMinutes(60);

        // Act
        var evaluation = service.Observe(
            CreateRankGameweek(entryTwoRank: 2, entryFourRank: 4, 60),
            [_firstTarget]);

        // Assert
        evaluation.Outcome.Should().Be(FplLiveNotificationOutcome.NothingInteresting);
        evaluation.Digests.Should().BeEmpty();
    }

    private FplLiveNotificationService CreateService()
    {
        return new FplLiveNotificationService(
            _options,
            new FplLiveHighlightDetectionService(_options),
            new SqliteFplLiveNotificationStateStore(_databasePath),
            _timeProvider);
    }

    private static FplLiveGameweek CreateBenchGameweek(int benchPoints)
    {
        var managers = FplLiveTestData.CreateGameweek().Managers.ToArray();
        managers[0] = managers[0] with { BenchPoints = benchPoints };
        return FplLiveTestData.CreateGameweek(managers);
    }

    private static FplLiveGameweek CreateCaptainGameweek(int effectivePoints)
    {
        return CreateBenchAndCaptainGameweek(0, effectivePoints, 0);
    }

    private static FplLiveGameweek CreateBenchAndCaptainGameweek(
        int benchPoints,
        int captainEffectivePoints,
        int capturedMinute)
    {
        var managers = FplLiveTestData.CreateGameweek().Managers.ToArray();
        managers[0] = managers[0] with
        {
            BenchPoints = benchPoints,
            Captain = managers[0].Captain with
            {
                CaptainEffectivePoints = captainEffectivePoints
            }
        };
        return FplLiveTestData.CreateGameweek(
            managers,
            captainSuccessEntryIds: captainEffectivePoints >= 20 ? [1] : [],
            capturedAtUtc: FplLiveTestData.CapturedAtUtc.AddMinutes(capturedMinute));
    }

    private static FplLiveGameweek CreateRankGameweek(
        int entryTwoRank,
        int entryFourRank,
        int capturedMinute)
    {
        var managers = FplLiveTestData.CreateGameweek().Managers.ToArray();
        managers[1] = managers[1] with { LiveRank = entryTwoRank };
        managers[3] = managers[3] with { LiveRank = entryFourRank };
        return FplLiveTestData.CreateGameweek(
            managers,
            capturedAtUtc: FplLiveTestData.CapturedAtUtc.AddMinutes(capturedMinute));
    }

    private static FplLiveGameweek CreateLeaderGameweek(
        int leaderEntryId,
        int capturedMinute)
    {
        var managers = FplLiveTestData.CreateGameweek().Managers
            .Select(manager => manager with
            {
                LiveRank = manager.EntryId == leaderEntryId
                    ? 1
                    : manager.EntryId < leaderEntryId
                        ? manager.EntryId + 1
                        : manager.EntryId,
                LiveTotalPoints = manager.EntryId == leaderEntryId
                    ? 125
                    : manager.LiveTotalPoints,
                GapToLeader = manager.EntryId == leaderEntryId
                    ? 0
                    : 125 - manager.LiveTotalPoints
            })
            .ToArray();
        return FplLiveTestData.CreateGameweek(
            managers,
            capturedAtUtc: FplLiveTestData.CapturedAtUtc.AddMinutes(capturedMinute));
    }

    private static FplLiveGameweek CreateScoreGameweek(
        int capturedMinute,
        params int[] scores)
    {
        var managers = FplLiveTestData.CreateGameweek().Managers.ToArray();
        scores.Should().HaveCount(managers.Length);
        var leaderPoints = scores.Max();
        var rankByScore = scores
            .OrderByDescending(score => score)
            .Select((score, index) => (score, index))
            .GroupBy(item => item.score)
            .ToDictionary(group => group.Key, group => group.Min(item => item.index) + 1);

        for (var index = 0; index < managers.Length; index++)
        {
            managers[index] = managers[index] with
            {
                LiveRank = rankByScore[scores[index]],
                LiveTotalPoints = scores[index],
                GapToLeader = leaderPoints - scores[index]
            };
        }

        return FplLiveTestData.CreateGameweek(
            managers,
            capturedAtUtc: FplLiveTestData.CapturedAtUtc.AddMinutes(capturedMinute));
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
