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
