using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Live;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Live;

[TestFixture]
public sealed class SqliteFplLiveNotificationStateStoreTests
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
        _databasePath = Path.Combine(_testDirectory, "fpl-live.db");
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
    public void Save_RecreatedStore_RoundTripsSnapshotTargetsAndHighlightTypes()
    {
        // Arrange
        var detectedAt = FplLiveTestData.CapturedAtUtc;
        var gameweek = FplLiveTestData.CreateGameweek() with
        {
            SwingInsights =
            [
                new UniqueRemainingPlayerInsight(1, "Alpha", "Salah"),
                new CaptainClashInsight(1, "Alpha", "Salah", 2, "Beta", "Haaland")
            ]
        };
        FplLiveHighlight[] highlights =
        [
            new LeaderChangedHighlight(1, "Alpha", 2, "Beta", 123, 3, detectedAt),
            new SignificantRankChangeHighlight(3, "Gamma", 5, 2, detectedAt),
            new BenchThresholdReachedHighlight(1, "Alpha", 12, detectedAt),
            new CaptainSuccessHighlight(2, "Beta", "Salah", 24, detectedAt),
            new CaptainDisasterHighlight(
                4,
                "Delta",
                "Haaland",
                1,
                "Palmer",
                9,
                detectedAt),
            new AutomaticSubstitutionHighlight(
                1,
                "Alpha",
                "Out",
                "In",
                8,
                detectedAt)
        ];
        var state = new FplLiveNotificationState(
            123,
            "2026/27",
            3,
            gameweek,
            [
                new FplLiveTargetNotificationState(
                    10,
                    100,
                    detectedAt.AddHours(-1),
                    4,
                    highlights),
                new FplLiveTargetNotificationState(20, 200, null, 1, [])
            ]);
        var store = new SqliteFplLiveNotificationStateStore(_databasePath);

        // Act
        store.Save(state);
        var restartedStore = new SqliteFplLiveNotificationStateStore(_databasePath);
        var restored = restartedStore.Get(123, "2026/27", 3);

        // Assert
        restored.Should().NotBeNull();
        restored!.LastObservedSnapshot.Should().BeEquivalentTo(gameweek);
        restored.Targets.Should().HaveCount(2);
        restored.Targets[0].LastPublishedAtUtc.Should().Be(detectedAt.AddHours(-1));
        restored.Targets[0].NextDigestSequence.Should().Be(4);
        restored.Targets[0].PendingHighlights.Should().BeEquivalentTo(highlights);
        restored.Targets[1].PendingHighlights.Should().BeEmpty();
    }

    [Test]
    public void Save_UpdatedState_ReplacesRemovedPendingHighlights()
    {
        // Arrange
        var gameweek = FplLiveTestData.CreateGameweek();
        var store = new SqliteFplLiveNotificationStateStore(_databasePath);
        var target = new FplLiveTargetNotificationState(
            10,
            100,
            null,
            1,
            [new BenchThresholdReachedHighlight(
                1,
                "Alpha",
                8,
                FplLiveTestData.CapturedAtUtc)]);
        var state = new FplLiveNotificationState(
            123,
            "2026/27",
            3,
            gameweek,
            [target]);
        store.Save(state);

        // Act
        store.Save(state with
        {
            Targets =
            [
                target with
                {
                    LastPublishedAtUtc = FplLiveTestData.CapturedAtUtc,
                    NextDigestSequence = 2,
                    PendingHighlights = []
                }
            ]
        });

        // Assert
        var restored = store.Get(123, "2026/27", 3)!;
        restored.Targets[0].PendingHighlights.Should().BeEmpty();
        restored.Targets[0].NextDigestSequence.Should().Be(2);
    }
}
