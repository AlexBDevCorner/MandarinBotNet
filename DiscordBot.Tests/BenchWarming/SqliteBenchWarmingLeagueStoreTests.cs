using AwesomeAssertions;
using DiscordBot.BenchWarming;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DiscordBot.Tests.BenchWarming;

[TestFixture]
public sealed class SqliteBenchWarmingLeagueStoreTests
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
        _databasePath = Path.Combine(_testDirectory, "bench-warming-league.db");
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
    public void IsRoundCalculated_AfterSaveRound_ReturnsTrue()
    {
        // Arrange
        var store = CreateStore();
        store.SaveRound(
            "2026/27",
            5,
            [CreatePlayerPoints(entryId: 100, playerId: 1, points: 12)],
            DateTimeOffset.UtcNow);

        // Act
        var isCalculated = store.IsRoundCalculated("2026/27", 5);

        // Assert
        isCalculated.Should().BeTrue();
    }

    [Test]
    public void IsRoundCalculated_OtherSeasonOrEvent_ReturnsFalse()
    {
        // Arrange
        var store = CreateStore();
        store.SaveRound(
            "2026/27",
            5,
            [CreatePlayerPoints(entryId: 100, playerId: 1, points: 12)],
            DateTimeOffset.UtcNow);

        // Act
        var otherSeason = store.IsRoundCalculated("2027/28", 5);
        var otherEvent = store.IsRoundCalculated("2026/27", 6);

        // Assert
        otherSeason.Should().BeFalse();
        otherEvent.Should().BeFalse();
    }

    [Test]
    public void GetSeasonStandings_MultipleRoundsAndEntries_SumsPerEntryOrderedByPoints()
    {
        // Arrange
        var store = CreateStore();
        store.SaveRound(
            "2026/27",
            5,
            [
                CreatePlayerPoints(entryId: 100, entryName: "Team A", playerId: 1, points: 12),
                CreatePlayerPoints(entryId: 200, entryName: "Team B", playerId: 2, points: 5)
            ],
            DateTimeOffset.UtcNow);
        store.SaveRound(
            "2026/27",
            6,
            [
                CreatePlayerPoints(entryId: 100, entryName: "Team A", playerId: 3, points: 8),
                CreatePlayerPoints(entryId: 200, entryName: "Team B", playerId: 4, points: 30)
            ],
            DateTimeOffset.UtcNow);

        // Act
        var standings = store.GetSeasonStandings("2026/27");

        // Assert
        standings.Should().BeEquivalentTo(
            [
                new BenchWarmingEntryStanding(200, "Team B", 35),
                new BenchWarmingEntryStanding(100, "Team A", 20)
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public void GetSeasonStandings_TeamRenamed_AggregatesByEntryIdAndUsesLatestName()
    {
        // Arrange
        var store = CreateStore();
        store.SaveRound(
            "2026/27",
            2,
            [
                new BenchWarmingEntryRoundStanding(2, 100, "Old Name", 5),
                new BenchWarmingEntryRoundStanding(2, 200, "Other Team", 9)
            ],
            [],
            DateTimeOffset.UtcNow);
        store.SaveRound(
            "2026/27",
            3,
            [
                new BenchWarmingEntryRoundStanding(3, 100, "New Name", 8),
                new BenchWarmingEntryRoundStanding(3, 200, "Other Team", 4)
            ],
            [],
            DateTimeOffset.UtcNow);

        // Act
        var standings = store.GetSeasonStandings("2026/27");

        // Assert
        standings.Should().BeEquivalentTo(
            [
                new BenchWarmingEntryStanding(100, "New Name", 13),
                new BenchWarmingEntryStanding(200, "Other Team", 13)
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public void GetTrackingInfo_LegacyRoundsWithoutEntryRounds_AreNotReportedAsComplete()
    {
        // Arrange: simulate an old-format database that predates bench_warming_entry_rounds.
        CreateLegacyDatabase(_databasePath, "2026/27", eventId: 5);
        var store = CreateStore();

        // Act
        var tracking = store.GetTrackingInfo("2026/27");
        var seasonStandings = store.GetSeasonStandings("2026/27");

        // Assert: the historical GW is not silently reported as a complete, tracked GW.
        tracking.Should().BeNull();
        seasonStandings.Should().BeEmpty();
    }

    private static void CreateLegacyDatabase(string databasePath, string season, int eventId)
    {
        var directory = Path.GetDirectoryName(databasePath)
            ?? throw new InvalidOperationException("The database path must include a directory.");
        Directory.CreateDirectory(directory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using (var roundsCommand = connection.CreateCommand())
        {
            roundsCommand.CommandText =
                """
                CREATE TABLE bench_warming_rounds (
                    season TEXT NOT NULL,
                    event_id INTEGER NOT NULL,
                    calculated_at_utc TEXT NOT NULL,
                    PRIMARY KEY (season, event_id)
                ) WITHOUT ROWID;
                """;
            roundsCommand.ExecuteNonQuery();
        }

        using (var pointsCommand = connection.CreateCommand())
        {
            pointsCommand.CommandText =
                """
                CREATE TABLE bench_warming_bench_points (
                    season TEXT NOT NULL,
                    event_id INTEGER NOT NULL,
                    entry_id INTEGER NOT NULL,
                    entry_name TEXT NOT NULL,
                    player_id INTEGER NOT NULL,
                    player_web_name TEXT NOT NULL,
                    points INTEGER NOT NULL,
                    PRIMARY KEY (season, event_id, entry_id, player_id)
                ) WITHOUT ROWID;
                """;
            pointsCommand.ExecuteNonQuery();
        }

        using (var insertRound = connection.CreateCommand())
        {
            insertRound.CommandText =
                "INSERT INTO bench_warming_rounds (season, event_id, calculated_at_utc) " +
                "VALUES ($season, $event_id, $calculated_at_utc);";
            insertRound.Parameters.AddWithValue("$season", season);
            insertRound.Parameters.AddWithValue("$event_id", eventId);
            insertRound.Parameters.AddWithValue(
                "$calculated_at_utc",
                DateTime.UtcNow.ToString("O"));
            insertRound.ExecuteNonQuery();
        }

        // Team A benched players exist; a historical Bench Boost manager (Team B)
        // stored no Multiplier == 0 players, so there is nothing to backfill for it.
        using (var insertPoints = connection.CreateCommand())
        {
            insertPoints.CommandText =
                "INSERT INTO bench_warming_bench_points " +
                "(season, event_id, entry_id, entry_name, player_id, player_web_name, points) " +
                "VALUES ($season, $event_id, $entry_id, $entry_name, $player_id, $player_web_name, $points);";
            insertPoints.Parameters.AddWithValue("$season", season);
            insertPoints.Parameters.AddWithValue("$event_id", eventId);
            insertPoints.Parameters.AddWithValue("$entry_id", 100);
            insertPoints.Parameters.AddWithValue("$entry_name", "Team A");
            insertPoints.Parameters.AddWithValue("$player_id", 1);
            insertPoints.Parameters.AddWithValue("$player_web_name", "Player 1");
            insertPoints.Parameters.AddWithValue("$points", 12);
            insertPoints.ExecuteNonQuery();
        }
    }

    [Test]
    public void GetSeasonStandings_NewSeasonKey_StartsWithEmptyStandings()
    {
        // Arrange
        var store = CreateStore();
        store.SaveRound(
            "2026/27",
            5,
            [CreatePlayerPoints(entryId: 100, playerId: 1, points: 12)],
            DateTimeOffset.UtcNow);

        // Act
        var standings = store.GetSeasonStandings("2027/28");

        // Assert
        standings.Should().BeEmpty();
    }

    [Test]
    public void GetLatestSeason_AfterMultipleSeasons_ReturnsMostRecent()
    {
        // Arrange
        var store = CreateStore();
        store.SaveRound(
            "2025/26",
            38,
            [CreatePlayerPoints(entryId: 100, playerId: 1, points: 12)],
            DateTimeOffset.UtcNow);
        store.SaveRound(
            "2026/27",
            5,
            [CreatePlayerPoints(entryId: 100, playerId: 2, points: 3)],
            DateTimeOffset.UtcNow);

        // Act
        var latestSeason = store.GetLatestSeason();

        // Assert
        latestSeason.Should().Be("2026/27");
    }

    [Test]
    public void GetLatestSeason_EmptyStore_ReturnsNull()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var latestSeason = store.GetLatestSeason();

        // Assert
        latestSeason.Should().BeNull();
    }

    [Test]
    public void GetSeasonRoundStandings_SumsPlayersPerEntryAndRound_OrderedDeterministically()
    {
        // Arrange
        var store = CreateStore();
        store.SaveRound(
            "2026/27",
            5,
            [
                CreatePlayerPoints(entryId: 100, entryName: "Team A", playerId: 1, points: 8),
                CreatePlayerPoints(entryId: 100, entryName: "Team A", playerId: 2, points: 4),
                CreatePlayerPoints(entryId: 200, entryName: "Team B", playerId: 3, points: 5)
            ],
            DateTimeOffset.UtcNow);
        store.SaveRound(
            "2026/27",
            6,
            [
                CreatePlayerPoints(entryId: 100, entryName: "Team A", playerId: 4, points: 2)
            ],
            DateTimeOffset.UtcNow);
        store.SaveRound(
            "2027/28",
            5,
            [
                CreatePlayerPoints(entryId: 900, entryName: "Other", playerId: 9, points: 99)
            ],
            DateTimeOffset.UtcNow);

        // Act
        var standings = store.GetSeasonRoundStandings("2026/27");

        // Assert
        standings.Should().BeEquivalentTo(
            [
                new BenchWarmingEntryRoundStanding(5, 100, "Team A", 12),
                new BenchWarmingEntryRoundStanding(5, 200, "Team B", 5),
                new BenchWarmingEntryRoundStanding(6, 100, "Team A", 2)
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public void GetSeasonRoundStandings_IncludesBenchBoostManagerWithZeroPoints()
    {
        // Arrange
        var store = CreateStore();
        store.SaveRound(
            "2026/27",
            5,
            [
                new BenchWarmingEntryRoundStanding(5, 100, "Team A", 12),
                new BenchWarmingEntryRoundStanding(5, 200, "Team B", 0, ActiveChip: "bboost")
            ],
            [
                new BenchWarmingPlayerPoints(100, "Team A", 1, "Haaland", 12)
            ],
            DateTimeOffset.UtcNow);

        // Act
        var standings = store.GetSeasonRoundStandings("2026/27");

        // Assert
        standings.Should().Contain(
            new BenchWarmingEntryRoundStanding(5, 200, "Team B", 0, "bboost"));
        standings.Should().Contain(
            new BenchWarmingEntryRoundStanding(5, 100, "Team A", 12, null));
    }

    [Test]
    public void GetSeasonRoundStandings_OtherSeason_Excluded()
    {
        // Arrange
        var store = CreateStore();
        store.SaveRound(
            "2026/27",
            5,
            [CreatePlayerPoints(entryId: 100, playerId: 1, points: 12)],
            DateTimeOffset.UtcNow);
        store.SaveRound(
            "2027/28",
            5,
            [CreatePlayerPoints(entryId: 900, playerId: 9, points: 99)],
            DateTimeOffset.UtcNow);

        // Act
        var standings = store.GetSeasonRoundStandings("2027/28");

        // Assert
        standings.Should().BeEquivalentTo(
            [new BenchWarmingEntryRoundStanding(5, 900, "Test Team", 99)],
            options => options.WithStrictOrdering());
    }

    [Test]
    public void GetTrackingInfo_TrackedRounds_ReportsFirstLatestAndCount()
    {
        // Arrange
        var store = CreateStore();
        store.SaveRound(
            "2026/27",
            5,
            [CreatePlayerPoints(entryId: 100, playerId: 1, points: 1)],
            DateTimeOffset.UtcNow);
        store.SaveRound(
            "2026/27",
            6,
            [CreatePlayerPoints(entryId: 100, playerId: 2, points: 1)],
            DateTimeOffset.UtcNow);
        store.SaveRound(
            "2026/27",
            8,
            [CreatePlayerPoints(entryId: 100, playerId: 3, points: 1)],
            DateTimeOffset.UtcNow);

        // Act
        var tracking = store.GetTrackingInfo("2026/27");

        // Assert
        tracking.Should().Be(new BenchWarmingTrackingInfo(5, 8, 3));
    }

    [Test]
    public void GetTrackingInfo_EmptySeason_ReturnsNull()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var tracking = store.GetTrackingInfo("2026/27");

        // Assert
        tracking.Should().BeNull();
    }

    [Test]
    public void GetRoundBenchPoints_ReturnsOnlyRequestedSeasonAndEvent()
    {
        // Arrange
        var store = CreateStore();
        store.SaveRound(
            "2026/27",
            5,
            [
                CreatePlayerPoints(entryId: 100, entryName: "Team A", playerId: 1, points: 8),
                CreatePlayerPoints(entryId: 200, entryName: "Team B", playerId: 2, points: 3)
            ],
            DateTimeOffset.UtcNow);
        store.SaveRound(
            "2026/27",
            6,
            [CreatePlayerPoints(entryId: 100, entryName: "Team A", playerId: 3, points: 2)],
            DateTimeOffset.UtcNow);
        store.SaveRound(
            "2027/28",
            5,
            [CreatePlayerPoints(entryId: 900, entryName: "Other", playerId: 9, points: 99)],
            DateTimeOffset.UtcNow);

        // Act
        var benchPoints = store.GetRoundBenchPoints("2026/27", 5);

        // Assert
        benchPoints.Should().BeEquivalentTo(
            [
                new BenchWarmingPlayerPoints(100, "Team A", 1, "Player 1", 8),
                new BenchWarmingPlayerPoints(200, "Team B", 2, "Player 2", 3)
            ],
            options => options.WithStrictOrdering());
    }

    private SqliteBenchWarmingLeagueStore CreateStore()
    {
        return new SqliteBenchWarmingLeagueStore(_databasePath);
    }

    private static BenchWarmingPlayerPoints CreatePlayerPoints(
        int entryId,
        int playerId,
        int points,
        string entryName = "Test Team")
    {
        return new BenchWarmingPlayerPoints(
            entryId,
            entryName,
            playerId,
            $"Player {playerId}",
            points);
    }
}
