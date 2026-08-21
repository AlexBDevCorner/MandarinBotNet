using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Historical;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Historical;

[TestFixture]
public sealed class SqliteFplStatisticsStoreTests
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
        _databasePath = Path.Combine(_testDirectory, "fpl-statistics.db");
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
    public void SaveSnapshot_InitializedDatabase_RoundTripsAllHistoricalData()
    {
        // Arrange
        var store = CreateStore();
        var snapshot = CreateSnapshot(
            "2026/27",
            5,
            CreateManager(100, "Team A", "Alice", benchPoints: 4));

        // Act
        store.SaveSnapshot(snapshot);
        var savedSnapshot = store.GetSnapshot("2026/27", 5);

        // Assert
        savedSnapshot.Should().BeEquivalentTo(snapshot);
        savedSnapshot!.Managers[0].Lineup[0].IsBench.Should().BeTrue();
        store.IsSnapshotStored("2026/27", 5).Should().BeTrue();
    }

    [Test]
    public void SaveSnapshot_RerecordingSameSeasonAndEvent_ReplacesRowsWithoutDuplicates()
    {
        // Arrange
        var store = CreateStore();
        store.SaveSnapshot(CreateSnapshot(
            "2026/27",
            5,
            CreateManager(100, "Team A", "Alice", benchPoints: 4),
            CreateManager(200, "Team B", "Bob", benchPoints: 2)));
        var replacement = CreateSnapshot(
            "2026/27",
            5,
            CreateManager(100, "Team A renamed", "Alice", eventScore: 20, benchPoints: 1));

        // Act
        store.SaveSnapshot(replacement);
        var snapshots = store.GetSnapshots("2026/27");

        // Assert
        snapshots.Should().ContainSingle().Which.Should().BeEquivalentTo(replacement);
    }

    [Test]
    public void GetSnapshots_ManagerFilter_ReturnsRequestedManagerAcrossGameweeks()
    {
        // Arrange
        var store = CreateStore();
        store.SaveSnapshot(CreateSnapshot(
            "2026/27",
            5,
            CreateManager(100, "Team A", "Alice"),
            CreateManager(200, "Team B", "Bob")));
        store.SaveSnapshot(CreateSnapshot(
            "2026/27",
            6,
            CreateManager(100, "Team A", "Alice", eventScore: 20)));

        // Act
        var history = store.GetSnapshots("2026/27", managerEntryId: 100);

        // Assert
        history.Should().HaveCount(2);
        history.Select(snapshot => snapshot.EventId).Should().Equal(5, 6);
        history.Should().OnlyContain(snapshot =>
            snapshot.Managers.Count == 1 && snapshot.Managers[0].EntryId == 100);
    }

    [Test]
    public void GetSnapshots_SeasonFilter_IsolatesNewSeasonWithSameEventId()
    {
        // Arrange
        var store = CreateStore();
        var firstSeason = CreateSnapshot(
            "2026/27",
            1,
            CreateManager(100, "Team A", "Alice", eventScore: 10));
        var secondSeason = CreateSnapshot(
            "2027/28",
            1,
            CreateManager(100, "Team A", "Alice", eventScore: 20));
        store.SaveSnapshot(firstSeason);
        store.SaveSnapshot(secondSeason);

        // Act
        var firstSeasonSnapshots = store.GetSnapshots("2026/27");
        var secondSeasonSnapshots = store.GetSnapshots("2027/28");

        // Assert
        firstSeasonSnapshots.Should().ContainSingle().Which.Should().BeEquivalentTo(firstSeason);
        secondSeasonSnapshots.Should().ContainSingle().Which.Should().BeEquivalentTo(secondSeason);
    }

    [Test]
    public void SaveSnapshot_StorageFailure_RollsBackThePreviousCompleteSnapshot()
    {
        // Arrange
        var store = CreateStore();
        var original = CreateSnapshot(
            "2026/27",
            5,
            CreateManager(100, "Team A", "Alice"));
        store.SaveSnapshot(original);
        CreateFailingLineupInsertTrigger();
        var replacement = CreateSnapshot(
            "2026/27",
            5,
            CreateManager(100, "Replacement", "Alice", eventScore: 30));

        // Act
        var act = () => store.SaveSnapshot(replacement);

        // Assert
        act.Should().Throw<SqliteException>();
        store.GetSnapshot("2026/27", 5).Should().BeEquivalentTo(original);
    }

    private SqliteFplStatisticsStore CreateStore()
    {
        return new SqliteFplStatisticsStore(_databasePath);
    }

    private void CreateFailingLineupInsertTrigger()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath
        }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TRIGGER fail_fpl_lineup_insert
            BEFORE INSERT ON fpl_lineup_picks
            BEGIN
                SELECT RAISE(ABORT, 'simulated storage failure');
            END;
            """;
        command.ExecuteNonQuery();
    }

    private static FplGameweekSnapshot CreateSnapshot(
        string season,
        int eventId,
        params FplManagerGameweekStatistics[] managers)
    {
        return new FplGameweekSnapshot(
            season,
            eventId,
            new DateTimeOffset(2026, 8, 15, 17, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 8, 16, 18, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 8, 17, 19, 0, 0, TimeSpan.FromHours(3)),
            managers);
    }

    private static FplManagerGameweekStatistics CreateManager(
        int entryId,
        string entryName,
        string managerName,
        int eventScore = 12,
        int benchPoints = 3)
    {
        return new FplManagerGameweekStatistics(
            entryId,
            entryName,
            managerName,
            eventScore,
            100,
            1,
            2,
            1,
            benchPoints,
            Lineup:
            [
                new FplLineupPick(
                    PlayerId: 1,
                    PlayerName: "Player One",
                    Position: 1,
                    Multiplier: 0,
                    IsCaptain: false,
                    IsViceCaptain: false,
                    Points: benchPoints),
                new FplLineupPick(
                    PlayerId: 2,
                    PlayerName: "Player Two",
                    Position: 2,
                    Multiplier: 2,
                    IsCaptain: true,
                    IsViceCaptain: false,
                    Points: eventScore)
            ]);
    }
}
