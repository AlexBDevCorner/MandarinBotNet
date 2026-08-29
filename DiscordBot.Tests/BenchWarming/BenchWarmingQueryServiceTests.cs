using AwesomeAssertions;
using DiscordBot.BenchWarming;
using NUnit.Framework;

namespace DiscordBot.Tests.BenchWarming;

[TestFixture]
public sealed class BenchWarmingQueryServiceTests
{
    [Test]
    public void GetSeasonOverview_NoLatestSeason_ReturnsNull()
    {
        var service = new BenchWarmingQueryService(
            new FakeStore { Season = null });

        service.GetSeasonOverview().Should().BeNull();
    }

    [Test]
    public void GetSeasonOverview_NoTrackingInfo_ReturnsNull()
    {
        var service = new BenchWarmingQueryService(
            new FakeStore { Season = "2026/27", Tracking = null });

        service.GetSeasonOverview().Should().BeNull();
    }

    [Test]
    public void GetSeasonOverview_SelectsLatestTrackedRound()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings =
            [
                new(2, 100, "Team A", 8),
                new(2, 200, "Team B", 3),
                new(5, 100, "Team A", 3),
                new(5, 200, "Team B", 7)
            ]
        });

        var overview = service.GetSeasonOverview()!;

        overview.LatestEventId.Should().Be(5);
        overview.LatestRoundStandings.Should().BeEquivalentTo(
            [
                new BenchWarmingEntryStanding(200, "Team B", 7),
                new BenchWarmingEntryStanding(100, "Team A", 3)
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public void GetSeasonOverview_AggregatesSeasonStandings()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings =
            [
                new(2, 100, "Team A", 8),
                new(2, 200, "Team B", 3),
                new(3, 100, "Team A", 12),
                new(3, 200, "Team B", 1),
                new(4, 100, "Team A", 9),
                new(4, 200, "Team B", 4),
                new(5, 100, "Team A", 3),
                new(5, 200, "Team B", 7)
            ]
        });

        var overview = service.GetSeasonOverview()!;

        overview.SeasonStandings.Should().BeEquivalentTo(
            [
                new BenchWarmingEntryStanding(100, "Team A", 32),
                new BenchWarmingEntryStanding(200, "Team B", 15)
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public void GetSeasonOverview_SelectsBiggestBenchDisasterWithTieBreak()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 6, 5),
            RoundStandings =
            [
                new(2, 100, "Team A", 8),
                new(4, 100, "Team A", 19),
                new(6, 200, "Team B", 19)
            ]
        });

        var overview = service.GetSeasonOverview()!;

        overview.Records.BiggestBenchDisaster.Should().Be(
            new BenchWarmingEntryRoundStanding(4, 100, "Team A", 19));
    }

    [Test]
    public void GetSeasonOverview_CalculatesAveragePerManagerRound()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings =
            [
                new(2, 100, "Team A", 8),
                new(2, 200, "Team B", 3),
                new(3, 100, "Team A", 12),
                new(3, 200, "Team B", 1),
                new(4, 100, "Team A", 9),
                new(4, 200, "Team B", 4),
                new(5, 100, "Team A", 3),
                new(5, 200, "Team B", 7)
            ]
        });

        var overview = service.GetSeasonOverview()!;

        overview.Records.AveragePointsPerManagerRound.Should().BeApproximately(5.875, 0.0001);
    }

    [Test]
    public void GetSeasonOverview_PreservesTrackingStartGw()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(5, 8, 3),
            RoundStandings = [new(5, 100, "Team A", 1)]
        });

        service.GetSeasonOverview()!.Tracking.FirstEventId.Should().Be(5);
    }

    [Test]
    public void GetSeasonOverview_LongestEightPlusStreak_IsThree()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings =
            [
                new(2, 100, "Team A", 8),
                new(3, 100, "Team A", 12),
                new(4, 100, "Team A", 9),
                new(5, 100, "Team A", 3)
            ]
        });

        service.GetSeasonOverview()!.Records.LongestEightPlusStreak!.Length.Should().Be(3);
    }

    [Test]
    public void GetSeasonOverview_DisasterStreakResetsBelowThreshold()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 4, 3),
            RoundStandings =
            [
                new(2, 100, "Team A", 10),
                new(3, 100, "Team A", 3),
                new(4, 100, "Team A", 12)
            ]
        });

        service.GetSeasonOverview()!.Records.LongestEightPlusStreak!.Length.Should().Be(1);
    }

    [Test]
    public void GetSeasonOverview_DisasterStreakResetsAcrossMissingGw()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 4, 3),
            RoundStandings =
            [
                new(2, 100, "Team A", 10),
                new(4, 100, "Team A", 12)
            ]
        });

        service.GetSeasonOverview()!.Records.LongestEightPlusStreak!.Length.Should().Be(1);
    }

    [Test]
    public void GetSeasonOverview_CleanBenchStreak_IsTwo()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 4, 3),
            RoundStandings =
            [
                new(2, 100, "Team A", 0),
                new(3, 100, "Team A", 0),
                new(4, 100, "Team A", 4)
            ]
        });

        service.GetSeasonOverview()!.Records.LongestCleanBenchStreak!.Length.Should().Be(2);
    }

    [Test]
    public void GetTeam_ExactMatch_ReturnsProfile()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings =
            [
                new(2, 100, "Team A", 6),
                new(2, 200, "Team B", 4)
            ]
        });

        var result = service.GetTeam("Team A");

        result.Outcome.Should().Be(BenchWarmingTeamLookupOutcome.Available);
        result.Profile!.EntryName.Should().Be("Team A");
        result.Profile!.EntryId.Should().Be(100);
    }

    [Test]
    public void GetTeam_ExactCaseInsensitiveMatch_ReturnsProfile()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings = [new(2, 100, "Team A", 6)]
        });

        var result = service.GetTeam("team a");

        result.Outcome.Should().Be(BenchWarmingTeamLookupOutcome.Available);
        result.Profile!.EntryName.Should().Be("Team A");
    }

    [Test]
    public void GetTeam_UniquePartialMatch_ReturnsProfile()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings = [new(2, 100, "Bobrov FC", 6)]
        });

        var result = service.GetTeam("bob");

        result.Outcome.Should().Be(BenchWarmingTeamLookupOutcome.Available);
        result.Profile!.EntryName.Should().Be("Bobrov FC");
    }

    [Test]
    public void GetTeam_MissingTeam_ReturnsNotFound()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings = [new(2, 100, "Team A", 6)]
        });

        var result = service.GetTeam("ZZZ");

        result.Outcome.Should().Be(BenchWarmingTeamLookupOutcome.NotFound);
    }

    [Test]
    public void GetTeam_AmbiguousPartialMatch_ReturnsCandidates()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings =
            [
                new(2, 100, "Fraud United", 6),
                new(2, 200, "Maguire United", 4)
            ]
        });

        var result = service.GetTeam("United");

        result.Outcome.Should().Be(BenchWarmingTeamLookupOutcome.Ambiguous);
        result.Candidates.Should().BeEquivalentTo(
            [
                new BenchWarmingTeamCandidate(100, "Fraud United"),
                new BenchWarmingTeamCandidate(200, "Maguire United")
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public void GetTeam_IdenticalLatestNames_AreAmbiguous()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings =
            [
                new(2, 100, "United", 6),
                new(2, 200, "United", 4)
            ]
        });

        var result = service.GetTeam("United");

        result.Outcome.Should().Be(BenchWarmingTeamLookupOutcome.Ambiguous);
        result.Candidates.Should().BeEquivalentTo(
            [
                new BenchWarmingTeamCandidate(100, "United"),
                new BenchWarmingTeamCandidate(200, "United")
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public void GetTeam_NumericEntryId_ResolvesById()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings =
            [
                new(2, 100, "Team A", 6),
                new(2, 200, "Team B", 4)
            ]
        });

        var result = service.GetTeam("200");

        result.Outcome.Should().Be(BenchWarmingTeamLookupOutcome.Available);
        result.Profile!.EntryId.Should().Be(200);
    }

    [Test]
    public void GetTeam_NumericEntryId_UnknownId_ReturnsNotFound()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings = [new(2, 100, "Team A", 6)]
        });

        var result = service.GetTeam("999");

        result.Outcome.Should().Be(BenchWarmingTeamLookupOutcome.NotFound);
    }

    [Test]
    public void GetSeasonOverview_TeamRenamed_UsesLatestName()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings =
            [
                new(2, 100, "Old Maguire FC", 6),
                new(5, 100, "Maguire GOAT FC", 18)
            ]
        });

        var overview = service.GetSeasonOverview()!;

        overview.SeasonStandings.Should().ContainSingle(
            standing => standing.EntryId == 100);
        overview.SeasonStandings[0].EntryName.Should().Be("Maguire GOAT FC");
    }

    [Test]
    public void GetTeam_TeamRenamed_ProfileUsesLatestName()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings =
            [
                new(2, 100, "Old Maguire FC", 6),
                new(5, 100, "Maguire GOAT FC", 18)
            ]
        });

        var result = service.GetTeam("Maguire GOAT FC");

        result.Outcome.Should().Be(BenchWarmingTeamLookupOutcome.Available);
        result.Profile!.EntryName.Should().Be("Maguire GOAT FC");
    }

    [Test]
    public void GetTeam_LateJoiningManager_FirstTrackedEventIdIsPersonalFirstGw()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 8, 7),
            RoundStandings =
            [
                new(2, 100, "Early FC", 6),
                new(6, 300, "Late FC", 4),
                new(7, 300, "Late FC", 9),
                new(8, 300, "Late FC", 2)
            ]
        });

        var result = service.GetTeam("Late FC");

        result.Outcome.Should().Be(BenchWarmingTeamLookupOutcome.Available);
        result.Profile!.FirstTrackedEventId.Should().Be(6);
        result.Profile!.History.Should().HaveCount(3);
    }

    [Test]
    public void GetSeasonOverview_AverageIncludesBenchBoostZero()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 2, 1),
            RoundStandings =
            [
                new(2, 100, "Team A", 10, ActiveChip: null),
                new(2, 200, "Team B", 0, ActiveChip: "bboost")
            ]
        });

        var overview = service.GetSeasonOverview()!;

        overview.Records.AveragePointsPerManagerRound.Should().BeApproximately(5.0, 0.0001);
        overview.Records.LongestCleanBenchStreak.Should().BeNull();
    }

    [Test]
    public void GetRound_TrackedGwWithZeroBenchPlayerRows_RemainsAvailable()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings =
            [
                new(5, 100, "Team A", 0, ActiveChip: "bboost"),
                new(5, 200, "Team B", 5)
            ],
            BenchPoints = []
        });

        var result = service.GetRound(5);

        result.Outcome.Should().Be(BenchWarmingRoundSummaryOutcome.Available);
        result.Summary!.Standings.Should().Contain(
            new BenchWarmingEntryStanding(100, "Team A", 0));
    }

    [Test]
    public void GetTeam_ProfileCalculatesStatisticsAndHistory()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 6, 5),
            RoundStandings =
            [
                new(2, 100, "Team A", 6),
                new(3, 100, "Team A", 11),
                new(4, 100, "Team A", 18),
                new(5, 100, "Team A", 12),
                new(6, 100, "Team A", 0)
            ]
        });

        var profile = service.GetTeam("Team A").Profile!;

        profile.TotalPoints.Should().Be(47);
        profile.AveragePoints.Should().BeApproximately(9.4, 0.0001);
        profile.HighestRoundPoints.Should().Be(18);
        profile.HighestRoundEventId.Should().Be(4);
        profile.LongestEightPlusStreak.Should().Be(3);
        profile.LongestCleanBenchStreak.Should().Be(1);
        profile.History.Should().HaveCount(5);
        profile.History[0].EventId.Should().Be(2);
        profile.History[4].EventId.Should().Be(6);
    }

    [Test]
    public void GetRound_AvailableGw_ReturnsStandingsAndTopPlayer()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings =
            [
                new(5, 100, "Team A", 3),
                new(5, 200, "Team B", 1)
            ],
            BenchPoints =
            [
                new(100, "Team A", 1, "Palmer", 12),
                new(200, "Team B", 2, "Kepa", 1)
            ]
        });

        var result = service.GetRound(5);

        result.Outcome.Should().Be(BenchWarmingRoundSummaryOutcome.Available);
        result.Summary!.Standings.Should().BeEquivalentTo(
            [
                new BenchWarmingEntryStanding(100, "Team A", 3),
                new BenchWarmingEntryStanding(200, "Team B", 1)
            ],
            options => options.WithStrictOrdering());
        result.Summary!.TopBenchPlayer!.PlayerWebName.Should().Be("Palmer");
    }

    [Test]
    public void GetRound_BeforeTrackingStarted_ReturnsNotTracked()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings = [new(2, 100, "Team A", 3)]
        });

        var result = service.GetRound(1);

        result.Outcome.Should().Be(BenchWarmingRoundSummaryOutcome.NotTracked);
        result.Tracking!.FirstEventId.Should().Be(2);
    }

    [Test]
    public void GetRound_MissingGwInsideTrackingRange_ReturnsNotTracked()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings =
            [
                new(2, 100, "Team A", 3),
                new(3, 100, "Team A", 3),
                new(5, 100, "Team A", 3)
            ]
        });

        var result = service.GetRound(4);

        result.Outcome.Should().Be(BenchWarmingRoundSummaryOutcome.NotTracked);
    }

    [Test]
    public void GetRound_AfterLatestTrackedGw_ReturnsNotTracked()
    {
        var service = new BenchWarmingQueryService(new FakeStore
        {
            Season = "2026/27",
            Tracking = new BenchWarmingTrackingInfo(2, 5, 4),
            RoundStandings = [new(5, 100, "Team A", 3)]
        });

        var result = service.GetRound(6);

        result.Outcome.Should().Be(BenchWarmingRoundSummaryOutcome.NotTracked);
    }

    [Test]
    public void GetRound_NoSeason_ReturnsNoData()
    {
        var service = new BenchWarmingQueryService(new FakeStore { Season = null });

        service.GetRound(5).Outcome.Should().Be(BenchWarmingRoundSummaryOutcome.NoData);
    }

    private sealed class FakeStore : IBenchWarmingLeagueStore
    {
        public string? Season { get; init; }

        public BenchWarmingTrackingInfo? Tracking { get; init; }

        public IReadOnlyList<BenchWarmingEntryRoundStanding> RoundStandings { get; init; } =
            [];

        public IReadOnlyList<BenchWarmingPlayerPoints> BenchPoints { get; init; } = [];

        public bool IsRoundCalculated(string season, int eventId) =>
            RoundStandings.Any(standing => standing.EventId == eventId);

        public void SaveRound(
            string season,
            int eventId,
            IReadOnlyList<BenchWarmingPlayerPoints> benchPoints,
            DateTimeOffset calculatedAtUtc) =>
            throw new NotSupportedException();

        public void SaveRound(
            string season,
            int eventId,
            IReadOnlyList<BenchWarmingEntryRoundStanding> entryRounds,
            IReadOnlyList<BenchWarmingPlayerPoints> benchPoints,
            DateTimeOffset calculatedAtUtc) =>
            throw new NotSupportedException();

        public IReadOnlyList<BenchWarmingEntryStanding> GetSeasonStandings(string season) =>
            throw new NotSupportedException();

        public IReadOnlyList<BenchWarmingEntryRoundStanding> GetSeasonRoundStandings(
            string season) => RoundStandings;

        public BenchWarmingTrackingInfo? GetTrackingInfo(string season) => Tracking;

        public IReadOnlyList<BenchWarmingPlayerPoints> GetRoundBenchPoints(
            string season,
            int eventId) => BenchPoints;

        public string? GetLatestSeason() => Season;
    }
}
