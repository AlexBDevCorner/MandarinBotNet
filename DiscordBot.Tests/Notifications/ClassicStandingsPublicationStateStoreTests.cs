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
    public void GetPublicationState_NoState_ReturnsNull()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var result = store.GetPublicationState(10, 100, 123);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void SetPublicationState_RestartedStoreUsingSameDatabase_RetainsValue()
    {
        // Arrange
        var store = CreateStore();
        var updatedAt = new DateTimeOffset(2026, 10, 8, 17, 0, 0, TimeSpan.Zero);
        store.SetPublicationState(10, 100, 123, "fp-a", 1, updatedAt);

        // Act: simulate a restart preserving the SQLite volume.
        SqliteConnection.ClearAllPools();
        var restarted = CreateStore();

        // Assert
        var state = restarted.GetPublicationState(10, 100, 123);
        state!.Fingerprint.Should().Be("fp-a");
        state.Sequence.Should().Be(1);
    }

    [Test]
    public void SetPublicationState_DifferentChannels_RemainIndependent()
    {
        // Arrange
        var store = CreateStore();
        store.SetPublicationState(10, 100, 123, "fp-a", 1, DateTimeOffset.UtcNow);

        // Act
        store.SetPublicationState(10, 200, 123, "fp-b", 1, DateTimeOffset.UtcNow);

        // Assert
        store.GetPublicationState(10, 100, 123)!.Fingerprint.Should().Be("fp-a");
        store.GetPublicationState(10, 200, 123)!.Fingerprint.Should().Be("fp-b");
    }

    [Test]
    public void SetPublicationState_DifferentLeagues_RemainIndependent()
    {
        // Arrange
        var store = CreateStore();

        // Act
        store.SetPublicationState(10, 100, 123, "fp-a", 1, DateTimeOffset.UtcNow);
        store.SetPublicationState(10, 100, 456, "fp-b", 1, DateTimeOffset.UtcNow);

        // Assert
        store.GetPublicationState(10, 100, 123)!.Fingerprint.Should().Be("fp-a");
        store.GetPublicationState(10, 100, 456)!.Fingerprint.Should().Be("fp-b");
    }

    [Test]
    public void SetPublicationState_Overwrite_TracksLatestOnly()
    {
        // Arrange: A -> B -> A must recognize the final A as a change, so
        // only the latest snapshot is kept.
        var store = CreateStore();
        store.SetPublicationState(10, 100, 123, "fp-a", 1, DateTimeOffset.UtcNow);
        store.SetPublicationState(10, 100, 123, "fp-b", 2, DateTimeOffset.UtcNow);

        // Act
        store.SetPublicationState(10, 100, 123, "fp-a", 3, DateTimeOffset.UtcNow);

        // Assert
        store.GetPublicationState(10, 100, 123)!.Should().Match<ClassicStandingsPublicationState>(
            state => state.Fingerprint == "fp-a" && state.Sequence == 3);
    }

    [Test]
    public void SetPublicationState_SequenceRoundTrips()
    {
        // Arrange
        var store = CreateStore();

        // Act
        store.SetPublicationState(10, 100, 123, "fp-a", 7, DateTimeOffset.UtcNow);

        // Assert: the sequence survives so the next publication key is fresh.
        store.GetPublicationState(10, 100, 123)!.Sequence.Should().Be(7);
    }

    [Test]
    public void Constructor_DatabaseWithLegacyStateTable_MigratesSequenceColumn()
    {
        // Arrange: a database created before the publication sequence existed.
        Directory.CreateDirectory(_testDirectory);
        using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE classic_standings_publication_state (
                    guild_id TEXT NOT NULL,
                    channel_id TEXT NOT NULL,
                    league_id INTEGER NOT NULL,
                    fingerprint TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL,
                    PRIMARY KEY (
                        guild_id,
                        channel_id,
                        league_id
                    )
                ) WITHOUT ROWID;
                INSERT INTO classic_standings_publication_state (
                    guild_id,
                    channel_id,
                    league_id,
                    fingerprint,
                    updated_at_utc
                )
                VALUES ('10', '100', 123, 'fp-legacy', '2026-10-08T17:00:00.0000000Z');
                """;
            command.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();

        // Act
        var store = CreateStore();

        // Assert: the legacy baseline is preserved with sequence 0, and new
        // publications keep working.
        store.GetPublicationState(10, 100, 123)!.Should().Match<ClassicStandingsPublicationState>(
            state => state.Fingerprint == "fp-legacy" && state.Sequence == 0);
        store.SetPublicationState(10, 100, 123, "fp-new", 1, DateTimeOffset.UtcNow);
        store.GetPublicationState(10, 100, 123)!.Should().Match<ClassicStandingsPublicationState>(
            state => state.Fingerprint == "fp-new" && state.Sequence == 1);
    }

    private SqliteNotificationCheckpointStore CreateStore()
    {
        return new SqliteNotificationCheckpointStore(_databasePath);
    }
}
