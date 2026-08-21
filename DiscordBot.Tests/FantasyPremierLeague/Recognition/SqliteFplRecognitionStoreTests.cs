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
        var originalRating = CreateRating(fraudRating: 10);
        var rewrittenRating = originalRating with
        {
            FraudRating = 99,
            RuleVersion = "v2"
        };

        // Act
        store.Save(
            CreateRun(eventId: 1, ruleVersion: "v1"),
            new FplRecognitionResult([firstBlood, repeatedBenchWarmer], []));
        store.Save(
            CreateRun(eventId: 2, ruleVersion: "v2"),
            new FplRecognitionResult([laterFirstBlood], []));
        store.Save(
            CreateRun(eventId: 5, ruleVersion: "v1"),
            new FplRecognitionResult([], [originalRating]));
        store.Save(
            CreateRun(eventId: 5, ruleVersion: "v2"),
            new FplRecognitionResult([], [rewrittenRating]));

        // Assert
        store.GetAchievementAwards(123, "2026/27")
            .Should().HaveCount(2);
        store.GetAchievementAwards(123, "2026/27", eventId: 2)
            .Should().BeEmpty();
        store.GetManagerRatings(123, "2026/27", eventId: 5)
            .Should().ContainSingle().Which.Should().BeEquivalentTo(originalRating);
    }

    [Test]
    public void GetCompletedResult_EmptyRecognitionRun_ReturnsEmptyHistoricalResult()
    {
        // Arrange
        var store = new SqliteFplRecognitionStore(_databasePath);
        var run = CreateRun(eventId: 5, ruleVersion: "v1");
        store.Save(run, new FplRecognitionResult([], []));

        // Act
        var result = store.GetCompletedResult(123, "2026/27", 5);

        // Assert
        result.Should().NotBeNull();
        result!.Achievements.Should().BeEmpty();
        result.Ratings.Should().BeEmpty();
    }

    [Test]
    public void GetAwards_LeagueAndSeasonFilters_IsolatesHistoricalRecords()
    {
        // Arrange
        var store = new SqliteFplRecognitionStore(_databasePath);
        store.Save(
            CreateRun(123, "2026/27", 1, "v1"),
            new FplRecognitionResult(
                [CreateAward(123, "2026/27", 1, 10, "bench-warmer", true)],
                []));
        store.Save(
            CreateRun(456, "2026/27", 1, "v1"),
            new FplRecognitionResult(
                [CreateAward(456, "2026/27", 1, 10, "bench-warmer", true)],
                []));
        store.Save(
            CreateRun(123, "2027/28", 1, "v1"),
            new FplRecognitionResult(
                [CreateAward(123, "2027/28", 1, 10, "bench-warmer", true)],
                []));

        // Act
        var awards = store.GetAchievementAwards(123, "2026/27");

        // Assert
        awards.Should().ContainSingle().Which.LeagueId.Should().Be(123);
        awards.Single().Season.Should().Be("2026/27");
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

    private static FplManagerRating CreateRating(int fraudRating)
    {
        return new FplManagerRating(
            123,
            "2026/27",
            5,
            10,
            "Alpha",
            "Alice",
            1,
            fraudRating,
            42,
            "v1",
            new DateTimeOffset(2026, 8, 21, 19, 0, 0, TimeSpan.Zero));
    }

    private static FplRecognitionRun CreateRun(
        int leagueId = 123,
        string season = "2026/27",
        int eventId = 5,
        string ruleVersion = "v1")
    {
        return new FplRecognitionRun(
            leagueId,
            season,
            eventId,
            ruleVersion,
            new DateTimeOffset(2026, 8, 21, 19, 0, 0, TimeSpan.Zero));
    }
}
