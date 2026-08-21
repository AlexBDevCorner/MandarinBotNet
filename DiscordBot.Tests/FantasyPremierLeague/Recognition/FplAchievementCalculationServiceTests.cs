using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recognition;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Recognition;

[TestFixture]
public sealed class FplAchievementCalculationServiceTests
{
    [Test]
    public void Calculate_BoundaryRulesAndTiedWinner_ReturnsExpectedAwards()
    {
        // Arrange
        var options = new FantasyPremierLeagueOptions
        {
            LargeBenchPointsThreshold = 8,
            CaptainDisasterPointsThreshold = 2,
            CaptainDisasterViceCaptainPointsThreshold = 8,
            TransferCostAchievementThreshold = 8,
            RecognitionRuleVersion = "v1"
        };
        var snapshot = CreateSnapshot(
            1,
            CreateManager(
                10,
                "Alpha",
                eventScore: 50,
                rank: 1,
                benchPoints: 8,
                captainPlayerId: 1,
                captainPoints: 2,
                viceCaptainPlayerId: 2,
                viceCaptainPoints: 8,
                transferCost: 8),
            CreateManager(
                20,
                "Beta",
                eventScore: 50,
                rank: 2,
                benchPoints: 7,
                captainPlayerId: 3,
                captainPoints: 4,
                viceCaptainPlayerId: 4,
                viceCaptainPoints: 8,
                transferCost: 7),
            CreateManager(
                30,
                "Gamma",
                eventScore: 40,
                rank: 3,
                benchPoints: 8,
                captainPlayerId: 1,
                captainPoints: 1,
                viceCaptainPlayerId: 2,
                viceCaptainPoints: 8,
                transferCost: 8));
        var service = new FplAchievementCalculationService(options);

        // Act
        var awards = service.Calculate(
            123,
            snapshot,
            [snapshot],
            [],
            DateTimeOffset.UtcNow);

        // Assert
        awards.Should().HaveCount(9);
        awards.Select(award => (award.EntryName, award.AchievementKey))
            .Should().BeEquivalentTo(
            [
                ("Alpha", "first-blood"),
                ("Alpha", "bench-warmer"),
                ("Alpha", "captain-disaster"),
                ("Alpha", "minus-eight-enjoyer"),
                ("Beta", "first-blood"),
                ("Beta", "differential-merchant"),
                ("Gamma", "bench-warmer"),
                ("Gamma", "captain-disaster"),
                ("Gamma", "minus-eight-enjoyer")
            ]);
        awards.Where(award => award.AchievementKey == "first-blood")
            .Should().HaveCount(2);
        awards.Where(award => award.IsRepeatable)
            .Should().OnlyContain(award => award.EventId == snapshot.EventId);
    }

    [Test]
    public void Calculate_PreviousOneTimeAward_DoesNotAwardItAgain()
    {
        // Arrange
        var options = new FantasyPremierLeagueOptions();
        var firstSnapshot = CreateSnapshot(
            1,
            CreateManager(
                10,
                "Alpha",
                eventScore: 50,
                rank: 1,
                benchPoints: 0,
                captainPlayerId: 1,
                captainPoints: 5,
                viceCaptainPlayerId: 2,
                viceCaptainPoints: 5));
        var currentSnapshot = CreateSnapshot(
            2,
            CreateManager(
                10,
                "Alpha",
                eventScore: 70,
                rank: 1,
                benchPoints: 8,
                captainPlayerId: 1,
                captainPoints: 5,
                viceCaptainPlayerId: 2,
                viceCaptainPoints: 5));
        var existingAward = new FplAchievementAward(
            123,
            "2026/27",
            1,
            10,
            "Alpha",
            "first-blood",
            "First Blood",
            "Won the first recorded gameweek of the season.",
            IsRepeatable: false,
            "v1",
            DateTimeOffset.UtcNow);

        // Act
        var awards = new FplAchievementCalculationService(options).Calculate(
            123,
            currentSnapshot,
            [firstSnapshot, currentSnapshot],
            [existingAward],
            DateTimeOffset.UtcNow);

        // Assert
        awards.Select(award => award.AchievementKey)
            .Should().BeEquivalentTo("bench-warmer", "differential-merchant");
        awards.Should().NotContain(award => award.AchievementKey == "first-blood");
    }

    [Test]
    public void Calculate_MissingCaptainData_RejectsSnapshot()
    {
        // Arrange
        var manager = CreateManager(
            10,
            "Incomplete",
            eventScore: 50,
            rank: 1,
            benchPoints: 0,
            captainPlayerId: 1,
            captainPoints: 5,
            viceCaptainPlayerId: 2,
            viceCaptainPoints: 5) with
        {
            Lineup =
            [
                new FplLineupPick(1, "Captain", 1, 2, true, false, 5)
            ]
        };
        var snapshot = CreateSnapshot(1, manager);

        // Act
        var act = () => new FplAchievementCalculationService(
            new FantasyPremierLeagueOptions()).Calculate(
            123,
            snapshot,
            [snapshot],
            [],
            DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<InvalidDataException>().WithMessage("*captain and vice-captain*");
    }

    private static FplGameweekSnapshot CreateSnapshot(
        int eventId,
        params FplManagerGameweekStatistics[] managers)
    {
        return new FplGameweekSnapshot(
            "2026/27",
            eventId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            managers);
    }

    private static FplManagerGameweekStatistics CreateManager(
        int entryId,
        string entryName,
        int eventScore,
        int rank,
        int benchPoints,
        int captainPlayerId,
        int captainPoints,
        int viceCaptainPlayerId,
        int viceCaptainPoints,
        int transferCost = 0)
    {
        return new FplManagerGameweekStatistics(
            entryId,
            entryName,
            $"Manager {entryName}",
            eventScore,
            100,
            rank,
            rank,
            0,
            benchPoints,
            [
                new FplLineupPick(
                    captainPlayerId,
                    $"Captain {entryName}",
                    1,
                    2,
                    true,
                    false,
                    captainPoints),
                new FplLineupPick(
                    viceCaptainPlayerId,
                    $"Vice {entryName}",
                    2,
                    1,
                    false,
                    true,
                    viceCaptainPoints),
                new FplLineupPick(
                    entryId + 1_000,
                    $"Bench {entryName}",
                    12,
                    0,
                    false,
                    false,
                    benchPoints)
            ])
        {
            TransferCost = transferCost
        };
    }
}
