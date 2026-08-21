using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recap;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Recap;

[TestFixture]
public sealed class FplGameweekRecapCalculationServiceTests
{
    [Test]
    public void Calculate_MixedScoresAndMovement_ReturnsDeterministicMetricsAndAwards()
    {
        // Arrange
        var snapshot = CreateSnapshot(
            CreateManager(
                entryId: 30,
                entryName: "Zulu",
                eventScore: 30,
                rank: 5,
                lastRank: 2,
                benchPoints: 0,
                captainPoints: 5),
            CreateManager(
                entryId: 40,
                entryName: "Delta",
                eventScore: 50,
                rank: 4,
                lastRank: 6,
                benchPoints: 8,
                captainPoints: 6),
            CreateManager(
                entryId: 20,
                entryName: "Beta",
                eventScore: 80,
                rank: 2,
                lastRank: 2,
                benchPoints: 8,
                captainPoints: 15),
            CreateManager(
                entryId: 10,
                entryName: "Alpha",
                eventScore: 80,
                rank: 1,
                lastRank: 3,
                benchPoints: 8,
                captainPoints: 15));

        // Act
        var recap = new FplGameweekRecapCalculationService().Calculate(snapshot);

        // Assert
        recap.HighestScore.Should().Be(80);
        recap.HighestScorers.Select(manager => manager.EntryName)
            .Should().Equal("Alpha", "Beta");
        recap.LowestScore.Should().Be(30);
        recap.LowestScorers.Select(manager => manager.EntryName)
            .Should().Equal("Zulu");
        recap.AverageScore.Should().Be(60m);
        recap.BiggestClimb.Should().Be(2);
        recap.BiggestClimbers.Select(manager => manager.EntryName)
            .Should().Equal("Alpha", "Delta");
        recap.BiggestFall.Should().Be(-3);
        recap.BiggestFallers.Select(manager => manager.EntryName)
            .Should().Equal("Zulu");
        recap.NotableRankChanges.Select(manager => manager.EntryName)
            .Should().Equal("Zulu", "Alpha", "Delta");
        recap.Benchmasters.Select(manager => manager.EntryName)
            .Should().Equal("Alpha", "Beta", "Delta");
        recap.CaptainGeniuses.Select(performance => performance.Manager.EntryName)
            .Should().Equal("Alpha", "Beta");
    }

    [Test]
    public void Calculate_MissingCaptainData_RejectsIncompleteSnapshot()
    {
        // Arrange
        var manager = CreateManager(
            entryId: 10,
            entryName: "Incomplete",
            eventScore: 50,
            rank: 1,
            lastRank: 1,
            benchPoints: 0,
            captainPoints: 10) with
        {
            Lineup =
            [
                new FplLineupPick(
                    PlayerId: 1,
                    PlayerName: "No Captain",
                    Position: 1,
                    Multiplier: 1,
                    IsCaptain: false,
                    IsViceCaptain: false,
                    Points: 10)
            ]
        };

        // Act
        var act = () => new FplGameweekRecapCalculationService().Calculate(
            CreateSnapshot(manager));

        // Assert
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*captain data*");
    }

    [Test]
    public void Calculate_FirstGameweekWithoutPreviousRank_DoesNotInventMovement()
    {
        // Arrange
        var manager = CreateManager(
            entryId: 10,
            entryName: "First Gameweek",
            eventScore: 50,
            rank: 1,
            lastRank: 0,
            benchPoints: 0,
            captainPoints: 10);

        // Act
        var recap = new FplGameweekRecapCalculationService().Calculate(
            CreateSnapshot(manager));

        // Assert
        recap.BiggestClimb.Should().Be(0);
        recap.BiggestClimbers.Should().BeEmpty();
        recap.BiggestFall.Should().Be(0);
        recap.BiggestFallers.Should().BeEmpty();
        recap.NotableRankChanges.Should().BeEmpty();
    }

    private static FplGameweekSnapshot CreateSnapshot(
        params FplManagerGameweekStatistics[] managers)
    {
        return new FplGameweekSnapshot(
            "2026/27",
            5,
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
        int lastRank,
        int benchPoints,
        int captainPoints)
    {
        return new FplManagerGameweekStatistics(
            entryId,
            entryName,
            $"Manager {entryName}",
            eventScore,
            100,
            rank,
            lastRank,
            lastRank - rank,
            benchPoints,
            [
                new FplLineupPick(
                    entryId,
                    $"Captain {entryName}",
                    1,
                    2,
                    IsCaptain: true,
                    IsViceCaptain: false,
                    Points: captainPoints),
                new FplLineupPick(
                    entryId + 1_000,
                    $"Bench {entryName}",
                    12,
                    0,
                    IsCaptain: false,
                    IsViceCaptain: false,
                    Points: benchPoints)
            ]);
    }
}
