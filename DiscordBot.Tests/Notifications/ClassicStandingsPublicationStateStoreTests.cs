using AwesomeAssertions;
using DiscordBot.Notifications;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DiscordBot.Tests.Notifications;

[TestFixture]
public sealed class ClassicStandingsPublicationStateStoreTests
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
    public void GetLastPublishedFingerprint_NoState_ReturnsNull()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var result = store.GetLastPublishedFingerprint(10, 100, 123);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void SetLastPublishedFingerprint_RestartedStoreUsingSameDatabase_RetainsValue()
    {
        // Arrange
        var store = CreateStore();
        store.SetLastPublishedFingerprint(10, 100, 123, "fp-a", DateTimeOffset.UtcNow);

        // Act: simulate a restart preserving the SQLite volume.
        SqliteConnection.ClearAllPools();
        var restarted = CreateStore();

        // Assert
        restarted.GetLastPublishedFingerprint(10, 100, 123).Should().Be("fp-a");
    }

    [Test]
    public void SetLastPublishedFingerprint_DifferentChannels_RemainIndependent()
    {
        // Arrange
        var store = CreateStore();
        store.SetLastPublishedFingerprint(10, 100, 123, "fp-a", DateTimeOffset.UtcNow);

        // Act
        store.SetLastPublishedFingerprint(10, 200, 123, "fp-b", DateTimeOffset.UtcNow);

        // Assert
        store.GetLastPublishedFingerprint(10, 100, 123).Should().Be("fp-a");
        store.GetLastPublishedFingerprint(10, 200, 123).Should().Be("fp-b");
    }

    [Test]
    public void SetLastPublishedFingerprint_DifferentLeagues_RemainIndependent()
    {
        // Arrange
        var store = CreateStore();

        // Act
        store.SetLastPublishedFingerprint(10, 100, 123, "fp-a", DateTimeOffset.UtcNow);
        store.SetLastPublishedFingerprint(10, 100, 456, "fp-b", DateTimeOffset.UtcNow);

        // Assert
        store.GetLastPublishedFingerprint(10, 100, 123).Should().Be("fp-a");
        store.GetLastPublishedFingerprint(10, 100, 456).Should().Be("fp-b");
    }

    [Test]
    public void SetLastPublishedFingerprint_Overwrite_TracksLatestOnly()
    {
        // Arrange: A -> B -> A must recognize the final A as a change, so
        // only the latest fingerprint is kept.
        var store = CreateStore();
        store.SetLastPublishedFingerprint(10, 100, 123, "fp-a", DateTimeOffset.UtcNow);
        store.SetLastPublishedFingerprint(10, 100, 123, "fp-b", DateTimeOffset.UtcNow);

        // Act
        store.SetLastPublishedFingerprint(10, 100, 123, "fp-a", DateTimeOffset.UtcNow);

        // Assert
        store.GetLastPublishedFingerprint(10, 100, 123).Should().Be("fp-a");
    }

    private SqliteNotificationCheckpointStore CreateStore()
    {
        return new SqliteNotificationCheckpointStore(_databasePath);
    }
}
