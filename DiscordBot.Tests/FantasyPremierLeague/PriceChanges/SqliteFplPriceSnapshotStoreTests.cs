using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.PriceChanges;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.PriceChanges;

[TestFixture]
public sealed class SqliteFplPriceSnapshotStoreTests
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
        _databasePath = Path.Combine(_testDirectory, "fpl-price-snapshot.db");
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
    public void SaveSnapshot_NewStoreInstance_ReturnsPersistedPrices()
    {
        // Arrange
        var store = new SqliteFplPriceSnapshotStore(_databasePath);
        var prices = new Dictionary<int, int>
        {
            [1] = 100,
            [2] = 125
        };

        // Act
        store.SaveSnapshot(prices);
        var restartedStore = new SqliteFplPriceSnapshotStore(_databasePath);

        // Assert
        restartedStore.GetSnapshot().Should().NotBeNull();
        restartedStore.GetSnapshot()!.Prices.Should().BeEquivalentTo(prices);
        restartedStore.GetSnapshot()!.Version.Should().Be(1);
    }

    [Test]
    public void SaveSnapshot_ReplacesPreviousSnapshot()
    {
        // Arrange
        var store = new SqliteFplPriceSnapshotStore(_databasePath);
        store.SaveSnapshot(new Dictionary<int, int>
        {
            [1] = 100,
            [2] = 125
        });

        // Act
        store.SaveSnapshot(new Dictionary<int, int> { [1] = 105 });

        // Assert
        store.GetSnapshot()!.Prices.Should().BeEquivalentTo(
            new Dictionary<int, int> { [1] = 105 });
        store.GetSnapshot()!.Version.Should().Be(2);
    }

    [Test]
    public void SaveSnapshot_PriceChangeCheck_PersistsLatestChangesAcrossRestart()
    {
        // Arrange
        var checkedAtUtc = new DateTimeOffset(2026, 8, 30, 10, 15, 0, TimeSpan.Zero);
        var store = new SqliteFplPriceSnapshotStore(_databasePath);
        var check = new FplPriceChangeCheck(
            new Dictionary<int, int> { [1] = 101 },
            [new FplPlayerPriceChange(1, "Salah", 100, 101)],
            PreviousSnapshotVersion: 1,
            CheckedAtUtc: checkedAtUtc,
            CurrentEventId: 3);

        // Act
        store.SaveSnapshot(check);
        var restartedStore = new SqliteFplPriceSnapshotStore(_databasePath);

        // Assert
        restartedStore.GetLatestChanges().Should().NotBeNull();
        var batch = restartedStore.GetLatestChanges()!;
        batch.CheckedAtUtc.Should().Be(checkedAtUtc);
        batch.Changes.Should().Equal(check.Changes);
    }

    [Test]
    public void SaveSnapshot_QuietCheck_PreservesPreviousLatestChanges()
    {
        // Arrange
        var store = new SqliteFplPriceSnapshotStore(_databasePath);
        var firstCheck = new FplPriceChangeCheck(
            new Dictionary<int, int> { [1] = 101 },
            [new FplPlayerPriceChange(1, "Salah", 100, 101)],
            PreviousSnapshotVersion: 1,
            CheckedAtUtc: DateTimeOffset.Parse("2026-08-30T10:00:00Z"),
            CurrentEventId: 3);
        store.SaveSnapshot(firstCheck);
        var quietCheck = new FplPriceChangeCheck(
            new Dictionary<int, int> { [1] = 101 },
            [],
            PreviousSnapshotVersion: 2,
            CheckedAtUtc: DateTimeOffset.Parse("2026-08-30T11:00:00Z"),
            CurrentEventId: 3);

        // Act
        store.SaveSnapshot(quietCheck);

        // Assert
        store.GetLatestChanges()!.Changes.Should().Equal(firstCheck.Changes);
        store.GetSnapshot()!.Version.Should().Be(2);
    }
}
