using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Recognition;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Recognition;

[TestFixture]
public sealed class SqliteFplRecognitionStoreTests
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
        _databasePath = Path.Combine(_testDirectory, "fpl-recognition.db");
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
    public void Save_RetryAndHistoricalRewriteAttempt_PreservesOneRecordPerOccurrence()
    {
        // Arrange
        var store = new SqliteFplRecognitionStore(_databasePath);
        var firstBlood = CreateAward(
            eventId: 1,
            entryId: 10,
            achievementKey: "first-blood",
            isRepeatable: false);
        var repeatedBenchWarmer = CreateAward(
            eventId: 1,
            entryId: 10,
            achievementKey: "bench-warmer",
            isRepeatable: true);
        var laterFirstBlood = firstBlood with
        {
            EventId = 2,
            EntryName = "Renamed Alpha",
            RuleVersion = "v2"
        };

        // Act
        store.Save(
            CreateRun(eventId: 1, ruleVersion: "v1"),
            new FplRecognitionResult([firstBlood, repeatedBenchWarmer]));
        store.Save(
            CreateRun(eventId: 2, ruleVersion: "v2"),
            new FplRecognitionResult([laterFirstBlood]));

        // Assert
        store.GetAchievementAwards(123, "2026/27")
            .Should().HaveCount(2);
        store.GetAchievementAwards(123, "2026/27", eventId: 2)
            .Should().BeEmpty();
    }

    [Test]
    public void GetCompletedResult_EmptyRecognitionRun_ReturnsEmptyHistoricalResult()
    {
        // Arrange
        var store = new SqliteFplRecognitionStore(_databasePath);
        var run = CreateRun(eventId: 5, ruleVersion: "v1");
        store.Save(run, new FplRecognitionResult([]));

        // Act
        var result = store.GetCompletedResult(123, "2026/27", 5);

        // Assert
        result.Should().NotBeNull();
        result!.Achievements.Should().BeEmpty();
    }

    [Test]
    public void GetAwards_LeagueAndSeasonFilters_IsolatesHistoricalRecords()
    {
        // Arrange
        var store = new SqliteFplRecognitionStore(_databasePath);
        store.Save(
            CreateRun(123, "2026/27", 1, "v1"),
            new FplRecognitionResult(
                [CreateAward(123, "2026/27", 1, 10, "bench-warmer", true)]));
        store.Save(
            CreateRun(456, "2026/27", 1, "v1"),
            new FplRecognitionResult(
                [CreateAward(456, "2026/27", 1, 10, "bench-warmer", true)]));
        store.Save(
            CreateRun(123, "2027/28", 1, "v1"),
            new FplRecognitionResult(
                [CreateAward(123, "2027/28", 1, 10, "bench-warmer", true)]));

        // Act
        var awards = store.GetAchievementAwards(123, "2026/27");

        // Assert
        awards.Should().ContainSingle().Which.LeagueId.Should().Be(123);
        awards.Single().Season.Should().Be("2026/27");
    }

    [Test]
    public void GetLatestSeason_ReturnsMostRecentlyCalculatedRun()
    {
        // Arrange
        var store = new SqliteFplRecognitionStore(_databasePath);
        store.Save(
            CreateRun(123, "2026/27", 1, "v1", new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)),
            new FplRecognitionResult([CreateAward(123, "2026/27", 1, 10, "bench-warmer", true)]));
        store.Save(
            CreateRun(123, "2027/28", 3, "v1", new DateTimeOffset(2027, 8, 1, 0, 0, 0, TimeSpan.Zero)),
            new FplRecognitionResult([CreateAward(123, "2027/28", 3, 10, "bench-warmer", true)]));

        // Act
        var season = store.GetLatestSeason(123);

        // Assert
        season.Should().Be("2027/28");
    }

    [Test]
    public void GetLatestSeason_ZeroAchievementRunStillDeterminesLatestSeason()
    {
        // Arrange
        var store = new SqliteFplRecognitionStore(_databasePath);
        store.Save(
            CreateRun(123, "2026/27", 5, "v1", new DateTimeOffset(2026, 8, 5, 0, 0, 0, TimeSpan.Zero)),
            new FplRecognitionResult([]));

        // Act
        var season = store.GetLatestSeason(123);

        // Assert
        season.Should().Be("2026/27");
    }

    [Test]
    public void GetFirstCompletedEventId_ReturnsMinimumEventForLeagueAndSeason()
    {
        // Arrange
        var store = new SqliteFplRecognitionStore(_databasePath);
        store.Save(
            CreateRun(123, "2026/27", 5, "v1"),
            new FplRecognitionResult([CreateAward(123, "2026/27", 5, 10, "bench-warmer", true)]));
        store.Save(
            CreateRun(123, "2026/27", 3, "v1"),
            new FplRecognitionResult([CreateAward(123, "2026/27", 3, 10, "bench-warmer", true)]));
        store.Save(
            CreateRun(123, "2026/27", 7, "v1"),
            new FplRecognitionResult([CreateAward(123, "2026/27", 7, 10, "bench-warmer", true)]));

        // Act
        var eventId = store.GetFirstCompletedEventId(123, "2026/27");

        // Assert
        eventId.Should().Be(3);
    }

    [Test]
    public void GetFirstCompletedEventId_OtherLeagueDoesNotAffectResult()
    {
        // Arrange
        var store = new SqliteFplRecognitionStore(_databasePath);
        store.Save(
            CreateRun(123, "2026/27", 4, "v1"),
            new FplRecognitionResult([CreateAward(123, "2026/27", 4, 10, "bench-warmer", true)]));
        store.Save(
            CreateRun(999, "2026/27", 1, "v1"),
            new FplRecognitionResult([CreateAward(999, "2026/27", 1, 10, "bench-warmer", true)]));

        // Act
        var eventId = store.GetFirstCompletedEventId(123, "2026/27");

        // Assert
        eventId.Should().Be(4);
    }

    [Test]
    public void GetFirstCompletedEventId_OtherSeasonDoesNotAffectTrackingStart()
    {
        // Arrange
        var store = new SqliteFplRecognitionStore(_databasePath);
        store.Save(
            CreateRun(123, "2026/27", 4, "v1"),
            new FplRecognitionResult([CreateAward(123, "2026/27", 4, 10, "bench-warmer", true)]));
        store.Save(
            CreateRun(123, "2025/26", 1, "v1"),
            new FplRecognitionResult([CreateAward(123, "2025/26", 1, 10, "bench-warmer", true)]));

        // Act
        var eventId = store.GetFirstCompletedEventId(123, "2026/27");

        // Assert
        eventId.Should().Be(4);
    }

    [Test]
    public void GetLatestSeason_UnknownLeague_ReturnsNull()
    {
        var store = new SqliteFplRecognitionStore(_databasePath);

        store.GetLatestSeason(123).Should().BeNull();
    }

    [Test]
    public void GetFirstCompletedEventId_UnknownSeason_ReturnsNull()
    {
        var store = new SqliteFplRecognitionStore(_databasePath);

        store.GetFirstCompletedEventId(123, "2099/00").Should().BeNull();
    }

    [Test]
    public void GetLatestSeason_InvalidLeague_Throws()
    {
        var store = new SqliteFplRecognitionStore(_databasePath);

        store.Invoking(s => s.GetLatestSeason(0))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void GetFirstCompletedEventId_InvalidLeague_Throws()
    {
        var store = new SqliteFplRecognitionStore(_databasePath);

        store.Invoking(s => s.GetFirstCompletedEventId(0, "2026/27"))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void GetFirstCompletedEventId_InvalidSeason_Throws()
    {
        var store = new SqliteFplRecognitionStore(_databasePath);

        store.Invoking(s => s.GetFirstCompletedEventId(123, " "))
            .Should().Throw<ArgumentException>();
    }

    private static FplAchievementAward CreateAward(
        int leagueId = 123,
        string season = "2026/27",
        int eventId = 5,
        int entryId = 10,
        string achievementKey = "bench-warmer",
        bool isRepeatable = true)
    {
        return new FplAchievementAward(
            leagueId,
            season,
            eventId,
            entryId,
            "Alpha",
            achievementKey,
            achievementKey,
            "Test achievement",
            isRepeatable,
            "v1",
            new DateTimeOffset(2026, 8, 21, 19, 0, 0, TimeSpan.Zero));
    }

    private static FplRecognitionRun CreateRun(
        int leagueId = 123,
        string season = "2026/27",
        int eventId = 5,
        string ruleVersion = "v1",
        DateTimeOffset? calculatedAtUtc = null)
    {
        return new FplRecognitionRun(
            leagueId,
            season,
            eventId,
            ruleVersion,
            calculatedAtUtc ??
                new DateTimeOffset(2026, 8, 21, 19, 0, 0, TimeSpan.Zero));
    }
}
