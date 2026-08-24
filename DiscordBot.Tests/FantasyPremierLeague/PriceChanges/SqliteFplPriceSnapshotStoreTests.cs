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
}
