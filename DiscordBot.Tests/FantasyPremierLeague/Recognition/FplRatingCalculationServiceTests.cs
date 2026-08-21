using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recognition;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Recognition;

[TestFixture]
public sealed class FplRatingCalculationServiceTests
{
    [Test]
    public void Calculate_ComponentBoundaries_ProducesZeroAndMaximumFraudRatings()
    {
        // Arrange
        var snapshot = new FplGameweekSnapshot(
            "2026/27",
            5,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            [
                CreateManager(
                    10,
                    "Maximum Fraud",
                    rank: 2,
                    rankChange: -5,
                    benchPoints: 10,
                    captainPoints: 0,
                    viceCaptainPoints: 10,
                    transferCost: 8),
                CreateManager(
                    20,
                    "No Fraud",
                    rank: 1,
                    rankChange: 5,
                    benchPoints: 0,
                    captainPoints: 10,
                    viceCaptainPoints: 0,
                    transferCost: 0)
            ]);

        // Act
        var ratings = new FplRatingCalculationService(
            new FantasyPremierLeagueOptions { RecognitionRuleVersion = "v1" })
            .Calculate(123, snapshot, DateTimeOffset.UtcNow);

        // Assert
        ratings.Select(rating => rating.EntryName).Should().Equal("No Fraud", "Maximum Fraud");
        ratings.Single(rating => rating.EntryName == "Maximum Fraud")
            .FraudRating.Should().Be(100);
        ratings.Single(rating => rating.EntryName == "No Fraud")
            .FraudRating.Should().Be(0);
        ratings.Should().OnlyContain(rating =>
            rating.MaguireIndex >= 0 && rating.MaguireIndex <= 100);
    }

    [Test]
    public void Calculate_SameStoredInputs_ProducesDeterministicMaguireIndex()
    {
        // Arrange
        var snapshot = new FplGameweekSnapshot(
            "2026/27",
            5,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            [CreateManager(
                10,
                "Alpha",
                rank: 1,
                rankChange: 0,
                benchPoints: 3,
                captainPoints: 6,
                viceCaptainPoints: 4,
                transferCost: 2)]);
        var service = new FplRatingCalculationService(new FantasyPremierLeagueOptions());

        // Act
        var first = service.Calculate(123, snapshot, DateTimeOffset.UtcNow);
        var second = service.Calculate(123, snapshot, DateTimeOffset.UtcNow);

        // Assert
        first.Single().MaguireIndex.Should().Be(second.Single().MaguireIndex);
        first.Single().FraudRating.Should().Be(second.Single().FraudRating);
    }

    private static FplManagerGameweekStatistics CreateManager(
        int entryId,
        string entryName,
        int rank,
        int rankChange,
        int benchPoints,
        int captainPoints,
        int viceCaptainPoints,
        int transferCost)
    {
        return new FplManagerGameweekStatistics(
            entryId,
            entryName,
            $"Manager {entryName}",
            50,
            100,
            rank,
            rank - rankChange,
            rankChange,
            benchPoints,
            [
                new FplLineupPick(
                    entryId,
                    $"Captain {entryName}",
                    1,
                    2,
                    true,
                    false,
                    captainPoints),
                new FplLineupPick(
                    entryId + 1,
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
