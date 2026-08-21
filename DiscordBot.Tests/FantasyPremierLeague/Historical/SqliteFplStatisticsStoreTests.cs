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
            CreateManager(100, "Team A", "Alice", benchPoints: 4, transferCost: 8));

        // Act
        store.SaveSnapshot(snapshot);
        var savedSnapshot = store.GetSnapshot("2026/27", 5);

        // Assert
        savedSnapshot.Should().BeEquivalentTo(snapshot);
        savedSnapshot!.Managers[0].TransferCost.Should().Be(8);
        savedSnapshot.Managers[0].Lineup[0].IsBench.Should().BeTrue();
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
    public void Constructor_ExistingDatabaseWithoutTransferCost_MigratesAndPreservesRows()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath
        }.ToString();
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE fpl_gameweek_snapshots (
                    season TEXT NOT NULL,
                    event_id INTEGER NOT NULL,
                    deadline_utc TEXT NOT NULL,
                    standings_updated_at_utc TEXT NOT NULL,
                    captured_at_utc TEXT NOT NULL,
                    PRIMARY KEY (season, event_id)
                );
                CREATE TABLE fpl_manager_gameweek_stats (
                    season TEXT NOT NULL,
                    event_id INTEGER NOT NULL,
                    entry_id INTEGER NOT NULL,
                    entry_name TEXT NOT NULL,
                    manager_name TEXT NOT NULL,
                    event_score INTEGER NOT NULL,
                    total_score INTEGER NOT NULL,
                    rank INTEGER NOT NULL,
                    last_rank INTEGER NOT NULL,
                    rank_change INTEGER NOT NULL,
                    bench_points INTEGER NOT NULL,
                    PRIMARY KEY (season, event_id, entry_id),
                    FOREIGN KEY (season, event_id)
                        REFERENCES fpl_gameweek_snapshots (season, event_id)
                        ON DELETE CASCADE
                );
                CREATE TABLE fpl_lineup_picks (
                    season TEXT NOT NULL,
                    event_id INTEGER NOT NULL,
                    entry_id INTEGER NOT NULL,
                    player_id INTEGER NOT NULL,
                    player_name TEXT NOT NULL,
                    position INTEGER NOT NULL,
                    multiplier INTEGER NOT NULL,
                    is_captain INTEGER NOT NULL,
                    is_vice_captain INTEGER NOT NULL,
                    points INTEGER NOT NULL,
                    PRIMARY KEY (season, event_id, entry_id, player_id),
                    FOREIGN KEY (season, event_id, entry_id)
                        REFERENCES fpl_manager_gameweek_stats (season, event_id, entry_id)
                        ON DELETE CASCADE
                );
                INSERT INTO fpl_gameweek_snapshots
                    (season, event_id, deadline_utc, standings_updated_at_utc, captured_at_utc)
                VALUES
                    ('2026/27', 5, '2026-08-15T14:00:00.0000000+00:00',
                     '2026-08-16T15:00:00.0000000+00:00',
                     '2026-08-17T16:00:00.0000000+00:00');
                INSERT INTO fpl_manager_gameweek_stats
                    (season, event_id, entry_id, entry_name, manager_name, event_score,
                     total_score, rank, last_rank, rank_change, bench_points)
                VALUES
                    ('2026/27', 5, 100, 'Team A', 'Alice', 12, 100, 1, 2, 1, 3);
                INSERT INTO fpl_lineup_picks
                    (season, event_id, entry_id, player_id, player_name, position,
                     multiplier, is_captain, is_vice_captain, points)
                VALUES
                    ('2026/27', 5, 100, 1, 'Player One', 1, 0, 0, 0, 3),
                    ('2026/27', 5, 100, 2, 'Player Two', 2, 2, 1, 0, 12);
                """;
            command.ExecuteNonQuery();
        }

        // Act
        var store = CreateStore();
        var migratedSnapshot = store.GetSnapshot("2026/27", 5);

        // Assert
        migratedSnapshot.Should().NotBeNull();
        migratedSnapshot!.Managers.Should().ContainSingle();
        migratedSnapshot.Managers[0].TransferCost.Should().Be(0);

        var rewrittenSnapshot = migratedSnapshot with
        {
            Managers =
            [
                migratedSnapshot.Managers[0] with { TransferCost = 8 }
            ]
        };
        store.SaveSnapshot(rewrittenSnapshot);
        store.GetSnapshot("2026/27", 5)!.Managers[0].TransferCost.Should().Be(8);
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
        int benchPoints = 3,
        int transferCost = 0)
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
            ])
        {
            TransferCost = transferCost
        };
    }
}
