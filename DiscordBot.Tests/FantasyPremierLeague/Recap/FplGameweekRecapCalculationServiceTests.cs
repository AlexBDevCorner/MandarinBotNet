using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recap;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Recap;

[TestFixture]
public sealed class FplGameweekRecapCalculationServiceTests
{
    private static readonly string Season = "2026/27";

    [Test]
    public void Calculate_SingleWinner_HighlightsOnlyTheWinner()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 80, 1000, 1, 1),
            CreateManager(20, "Beta", 40, 990, 2, 2));

        var recap = Calculate(snapshot);

        recap.Highlights.OfType<FplGameweekWinnerHighlight>().Should().ContainSingle();
        var winner = recap.Highlights.OfType<FplGameweekWinnerHighlight>().Single();
        winner.Winners.Should().ContainSingle().Which.EntryName.Should().Be("Alpha");
        winner.Score.Should().Be(80);
        recap.Highlights.Should().ContainSingle();
    }

    [Test]
    public void Calculate_TiedWinners_BothAreWinners()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 70, 1000, 1, 2),
            CreateManager(20, "Beta", 70, 990, 2, 1),
            CreateManager(30, "Gamma", 50, 980, 3, 3));

        var recap = Calculate(snapshot);

        var winner = recap.Highlights.OfType<FplGameweekWinnerHighlight>().Single();
        winner.Winners.Select(manager => manager.EntryName)
            .Should().Equal("Alpha", "Beta");
    }

    [Test]
    public void Calculate_BiggestRankRise_SelectsClimb()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 5),
            CreateManager(20, "Beta", 60, 990, 2, 2));

        var recap = Calculate(snapshot);

        var movement = recap.Highlights.OfType<FplRankMovementHighlight>().Single();
        movement.Kind.Should().Be(FplRecapHighlightKind.BiggestClimb);
        movement.Manager.EntryName.Should().Be("Alpha");
        movement.PreviousRank.Should().Be(5);
        movement.CurrentRank.Should().Be(1);
    }

    [Test]
    public void Calculate_LastRankZero_IgnoredAsMovement()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 0),
            CreateManager(20, "Beta", 60, 990, 2, 2));

        var recap = Calculate(snapshot);

        recap.Highlights.OfType<FplRankMovementHighlight>().Should().BeEmpty();
    }

    [Test]
    public void Calculate_CaptainDisaster_SelectsLargestDifference()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 2, captainPoints: 2, viceCaptainPoints: 13),
            CreateManager(20, "Beta", 60, 990, 2, 1, captainPoints: 1, viceCaptainPoints: 8));

        var recap = Calculate(snapshot);

        var disaster = recap.Highlights.OfType<FplCaptainDisasterHighlight>().Single();
        disaster.Manager.EntryName.Should().Be("Alpha");
        disaster.Captain.PlayerName.Should().Be("Cap Alpha");
        disaster.Captain.Points.Should().Be(2);
        disaster.ViceCaptain.Points.Should().Be(13);
    }

    [Test]
    public void Calculate_CaptainDisaster_TieBrokenDeterministicallyByRank()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(20, "Beta", 60, 990, 2, 1, captainPoints: 2, viceCaptainPoints: 10),
            CreateManager(10, "Alpha", 60, 1000, 1, 2, captainPoints: 2, viceCaptainPoints: 10));

        var recap = Calculate(snapshot);

        var disaster = recap.Highlights.OfType<FplCaptainDisasterHighlight>().Single();
        disaster.Manager.EntryName.Should().Be("Alpha");
    }

    [Test]
    public void Calculate_NoCaptainDisaster_WhenThresholdNotMet()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 2, captainPoints: 5, viceCaptainPoints: 8));

        var recap = Calculate(snapshot);

        recap.Highlights.OfType<FplCaptainDisasterHighlight>().Should().BeEmpty();
    }

    [Test]
    public void Calculate_TransferHit_SelectsHighestCostThenLowestScore()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 2, transferCost: 8),
            CreateManager(20, "Beta", 39, 990, 2, 1, transferCost: 12));

        var recap = Calculate(snapshot);

        var transfer = recap.Highlights.OfType<FplTransferHitHighlight>().Single();
        transfer.Manager.EntryName.Should().Be("Beta");
        transfer.TransferCost.Should().Be(12);
    }

    [Test]
    public void Calculate_NoTransferHit_WhenBelowThreshold()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 2, transferCost: 4));

        var recap = Calculate(snapshot);

        recap.Highlights.OfType<FplTransferHitHighlight>().Should().BeEmpty();
    }

    [Test]
    public void Calculate_BenchDisaster_SelectsHighestBench()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 2, benchPoints: 8),
            CreateManager(20, "Beta", 60, 990, 2, 1, benchPoints: 21));

        var recap = Calculate(snapshot);

        var bench = recap.Highlights.OfType<FplBenchDisasterHighlight>().Single();
        bench.Manager.EntryName.Should().Be("Beta");
    }

    [Test]
    public void Calculate_NoBenchDisaster_WhenBelowThreshold()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 2, benchPoints: 5));

        var recap = Calculate(snapshot);

        recap.Highlights.OfType<FplBenchDisasterHighlight>().Should().BeEmpty();
    }

    [Test]
    public void Calculate_MaxHeadlineCount_NeverExceedsSix()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 90, 1000, 1, 4, benchPoints: 8, captainPoints: 2, viceCaptainPoints: 13, transferCost: 8),
            CreateManager(20, "Beta", 30, 990, 8, 2, benchPoints: 20, captainPoints: 1, viceCaptainPoints: 14, transferCost: 12),
            CreateManager(30, "Gamma", 50, 980, 2, 1));

        var recap = Calculate(snapshot);

        recap.Highlights.Count.Should().BeLessThanOrEqualTo(6);
        recap.Highlights.Should().ContainSingle(h => h is FplGameweekWinnerHighlight);
        recap.Highlights.OfType<FplRankMovementHighlight>()
            .Should().Contain(h => h.Kind == FplRecapHighlightKind.BiggestClimb);
        recap.Highlights.OfType<FplRankMovementHighlight>()
            .Should().Contain(h => h.Kind == FplRecapHighlightKind.BiggestFall);
        recap.Highlights.Should().ContainSingle(h => h is FplCaptainDisasterHighlight);
        recap.Highlights.Should().ContainSingle(h => h is FplTransferHitHighlight);
        recap.Highlights.Should().ContainSingle(h => h is FplBenchDisasterHighlight);
    }

    [Test]
    public void Calculate_FirstTimeAtTop_ReportedWhenNeverTopBefore()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 2));
        var history = new[]
        {
            CreateSnapshot(1, CreateManager(10, "Alpha", 55, 200, 2, 3)),
            CreateSnapshot(2, CreateManager(10, "Alpha", 55, 400, 2, 2)),
            CreateSnapshot(3, CreateManager(10, "Alpha", 55, 600, 2, 2)),
            CreateSnapshot(4, CreateManager(10, "Alpha", 55, 800, 2, 2))
        };

        var recap = Calculate(snapshot, history);

        var trend = recap.SeasonTrends.OfType<FplFirstTimeAtTopTrend>().Single();
        trend.Leader.EntryName.Should().Be("Alpha");
        trend.SinceTrackingStarted.Should().BeFalse();
    }

    [Test]
    public void Calculate_PreviouslyAtTop_NotReportedAsFirstTime()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 2));
        var history = new[]
        {
            CreateSnapshot(4, CreateManager(10, "Alpha", 55, 800, 1, 2))
        };

        var recap = Calculate(snapshot, history);

        recap.SeasonTrends.OfType<FplFirstTimeAtTopTrend>().Should().BeEmpty();
    }

    [Test]
    public void Calculate_ThreeWinsInLastFive_Reported()
    {
        var history = new[]
        {
            CreateSnapshot(1, CreateManager(10, "Alpha", 70, 100, 1, 1), CreateManager(20, "Beta", 40, 90, 2, 2)),
            CreateSnapshot(2, CreateManager(10, "Alpha", 40, 200, 1, 1), CreateManager(20, "Beta", 70, 190, 2, 2)),
            CreateSnapshot(3, CreateManager(10, "Alpha", 70, 300, 1, 1), CreateManager(20, "Beta", 40, 290, 2, 2)),
            CreateSnapshot(4, CreateManager(10, "Alpha", 40, 400, 1, 1), CreateManager(20, "Beta", 70, 390, 2, 2))
        };
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 70, 500, 1, 1),
            CreateManager(20, "Beta", 40, 490, 2, 2));

        var recap = Calculate(snapshot, history.Append(snapshot).ToArray());

        var trend = recap.SeasonTrends.OfType<FplRecentWinsTrend>().Single();
        trend.Manager.EntryName.Should().Be("Alpha");
        trend.WinCount.Should().Be(3);
        trend.Window.Should().Be(5);
    }

    [Test]
    public void Calculate_TiedWinner_ContributesToRecentWinCount()
    {
        var history = new[]
        {
            CreateSnapshot(1, CreateManager(10, "Alpha", 70, 100, 1, 1), CreateManager(20, "Beta", 40, 90, 2, 2)),
            CreateSnapshot(2, CreateManager(10, "Alpha", 40, 200, 1, 1), CreateManager(20, "Beta", 70, 190, 2, 2)),
            CreateSnapshot(3, CreateManager(10, "Alpha", 70, 300, 1, 1), CreateManager(20, "Beta", 40, 290, 2, 2)),
            CreateSnapshot(4, CreateManager(10, "Alpha", 40, 400, 1, 1), CreateManager(20, "Beta", 40, 390, 2, 2))
        };
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 70, 500, 1, 1),
            CreateManager(20, "Beta", 40, 490, 2, 2));

        var recap = Calculate(snapshot, history.Append(snapshot).ToArray());

        var trend = recap.SeasonTrends.OfType<FplRecentWinsTrend>().Single();
        trend.WinCount.Should().Be(4);
    }

    [Test]
    public void Calculate_RecentWins_WithHistoryGap_StillReported()
    {
        var history = new[]
        {
            CreateSnapshot(1, CreateManager(10, "Alpha", 40, 100, 3, 3), CreateManager(20, "Beta", 70, 200, 1, 1)),
            CreateSnapshot(2, CreateManager(10, "Alpha", 40, 200, 3, 3), CreateManager(20, "Beta", 70, 300, 1, 1)),
            CreateSnapshot(4, CreateManager(10, "Alpha", 70, 400, 1, 1), CreateManager(20, "Beta", 40, 390, 2, 2)),
            CreateSnapshot(5, CreateManager(10, "Alpha", 70, 500, 1, 1), CreateManager(20, "Beta", 40, 490, 2, 2)),
            CreateSnapshot(6, CreateManager(10, "Alpha", 40, 600, 2, 2), CreateManager(20, "Beta", 70, 590, 1, 1))
        };
        var snapshot = CreateSnapshot(
            7,
            CreateManager(10, "Alpha", 70, 700, 1, 1),
            CreateManager(20, "Beta", 40, 690, 2, 2));

        var recap = Calculate(snapshot, history.Append(snapshot).ToArray());

        var trend = recap.SeasonTrends.OfType<FplRecentWinsTrend>().Single();
        trend.Manager.EntryName.Should().Be("Alpha");
        trend.WinCount.Should().Be(3);
    }

    [Test]
    public void Calculate_ConsecutiveRankRises_Reported()
    {
        var history = new[]
        {
            CreateSnapshot(2, CreateManager(10, "Alpha", 55, 200, 4, 5)),
            CreateSnapshot(3, CreateManager(10, "Alpha", 55, 400, 3, 4)),
            CreateSnapshot(4, CreateManager(10, "Alpha", 55, 600, 2, 3))
        };
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 2),
            CreateManager(20, "Beta", 60, 990, 2, 1));

        var recap = Calculate(snapshot, history);

        var trend = recap.SeasonTrends.OfType<FplConsecutiveRankRisesTrend>().Single();
        trend.Manager.EntryName.Should().Be("Alpha");
        trend.StreakLength.Should().Be(4);
    }

    [Test]
    public void Calculate_HistoryGap_BreaksStreak()
    {
        var history = new[]
        {
            CreateSnapshot(2, CreateManager(10, "Alpha", 55, 200, 4, 5)),
            CreateSnapshot(3, CreateManager(10, "Alpha", 55, 400, 3, 4))
        };
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 2));

        var recap = Calculate(snapshot, history);

        recap.SeasonTrends.OfType<FplConsecutiveRankRisesTrend>().Should().BeEmpty();
    }

    [Test]
    public void Calculate_LeaderGapReduction_Reported()
    {
        var previous = CreateSnapshot(
            4,
            CreateManager(10, "Leader", 55, 980, 1, 1),
            CreateManager(20, "Chaser", 55, 950, 2, 2));
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Leader", 60, 1000, 1, 1),
            CreateManager(20, "Chaser", 60, 985, 2, 2));

        var recap = Calculate(snapshot, [previous]);

        var trend = recap.SeasonTrends.OfType<FplLeaderGapReductionTrend>().Single();
        trend.Manager.EntryName.Should().Be("Chaser");
        trend.PreviousGap.Should().Be(30);
        trend.CurrentGap.Should().Be(15);
    }

    [Test]
    public void Calculate_ManagerMissingFromPrevious_IgnoredForGap()
    {
        var previous = CreateSnapshot(
            4,
            CreateManager(10, "Leader", 55, 980, 1, 1),
            CreateManager(20, "Chaser", 55, 950, 2, 2));
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Leader", 60, 1000, 1, 1),
            CreateManager(30, "Newcomer", 60, 970, 2, 0),
            CreateManager(20, "Chaser", 60, 985, 3, 2));

        var recap = Calculate(snapshot, [previous]);

        var trend = recap.SeasonTrends.OfType<FplLeaderGapReductionTrend>().Single();
        trend.Manager.EntryName.Should().Be("Chaser");
    }

    [Test]
    public void Calculate_MaxTwoSeasonTrends_OnlyTopTwoReported()
    {
        var history = new[]
        {
            CreateSnapshot(1, CreateManager(10, "Alpha", 70, 100, 6, 6), CreateManager(20, "Beta", 40, 90, 7, 7)),
            CreateSnapshot(2, CreateManager(10, "Alpha", 70, 200, 4, 5), CreateManager(20, "Beta", 40, 190, 5, 6)),
            CreateSnapshot(3, CreateManager(10, "Alpha", 70, 300, 3, 4), CreateManager(20, "Beta", 40, 290, 4, 5)),
            CreateSnapshot(4, CreateManager(10, "Alpha", 70, 400, 2, 3), CreateManager(20, "Beta", 40, 300, 3, 4))
        };
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 2, benchPoints: 8, captainPoints: 2, viceCaptainPoints: 13),
            CreateManager(20, "Beta", 60, 970, 2, 3, benchPoints: 9, captainPoints: 1, viceCaptainPoints: 20));

        var recap = Calculate(snapshot, history.Append(snapshot).ToArray());

        recap.SeasonTrends.Count.Should().Be(2);
        recap.SeasonTrends[0].Should().BeOfType<FplFirstTimeAtTopTrend>();
        recap.SeasonTrends[1].Should().BeOfType<FplRecentWinsTrend>();
    }

    [Test]
    public void Calculate_FirstGameweekWithoutPreviousRank_DoesNotInventMovement()
    {
        var snapshot = CreateSnapshot(
            5,
            CreateManager(10, "Alpha", 60, 1000, 1, 0));

        var recap = Calculate(snapshot);

        recap.Highlights.OfType<FplRankMovementHighlight>().Should().BeEmpty();
        recap.Standings.Should().ContainSingle();
    }

    [Test]
    public void Calculate_MissingCaptainData_RejectsIncompleteSnapshot()
    {
        var manager = CreateManager(10, "Incomplete", 50, 100, 1, 1) with
        {
            Lineup =
            [
                new FplLineupPick(
                    1, "No Captain", 1, 1, IsCaptain: false, IsViceCaptain: false, Points: 10)
            ]
        };

        var act = () => Calculate(CreateSnapshot(5, manager));

        act.Should().Throw<InvalidDataException>().WithMessage("*captain data*");
    }

    private static FplGameweekRecap Calculate(
        FplGameweekSnapshot snapshot,
        params FplGameweekSnapshot[] history) => new FplGameweekRecapCalculationService(
            new FantasyPremierLeagueOptions()).Calculate(snapshot, history);

    private static FplGameweekSnapshot CreateSnapshot(
        int eventId,
        params FplManagerGameweekStatistics[] managers) => new(
            Season,
            eventId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            managers);

    private static FplManagerGameweekStatistics CreateManager(
        int entryId,
        string entryName,
        int eventScore,
        int totalScore,
        int rank,
        int lastRank,
        int benchPoints = 0,
        int captainPoints = 10,
        int viceCaptainPoints = 0,
        int transferCost = 0)
    {
        return new FplManagerGameweekStatistics(
            entryId,
            entryName,
            $"Manager {entryName}",
            eventScore,
            totalScore,
            rank,
            lastRank,
            lastRank - rank,
            benchPoints,
            [
                new FplLineupPick(
                    entryId,
                    $"Cap {entryName}",
                    1,
                    2,
                    IsCaptain: true,
                    IsViceCaptain: false,
                    Points: captainPoints),
                new FplLineupPick(
                    entryId + 1,
                    $"VC {entryName}",
                    2,
                    1,
                    IsCaptain: false,
                    IsViceCaptain: true,
                    Points: viceCaptainPoints),
                new FplLineupPick(
                    entryId + 2,
                    $"Bench {entryName}",
                    12,
                    0,
                    IsCaptain: false,
                    IsViceCaptain: false,
                    Points: benchPoints)
            ])
        {
            TransferCost = transferCost
        };
    }
}
