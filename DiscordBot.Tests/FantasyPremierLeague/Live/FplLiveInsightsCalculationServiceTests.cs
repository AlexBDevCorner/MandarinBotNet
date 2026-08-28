using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Live;
using DiscordBot.Responses;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Live;

[TestFixture]
public sealed class FplLiveInsightsCalculationServiceTests
{
    private static readonly DateTimeOffset SourceUpdatedAt =
        new(2026, 8, 21, 18, 45, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CapturedAt =
        new(2026, 8, 21, 18, 50, 0, TimeSpan.Zero);

    [Test]
    public void Calculate_LiveScoreDiffersFromOfficialOrder_UsesNetTotalsAndMovement()
    {
        // Arrange
        var fixture = new CalculationFixture();
        AddManager(
            fixture,
            101,
            "Alpha",
            "Alice",
            officialRank: 1,
            previousRank: 1,
            previousTotal: 1000,
            eventTotal: 30,
            transferCost: 0,
            new PickData(1, 1, 1, 2, 10, IsCaptain: true),
            new PickData(2, 2, 2, 1, 5, IsViceCaptain: true),
            new PickData(3, 3, 3, 1, 5));
        AddManager(
            fixture,
            202,
            "Beta",
            "Bob",
            officialRank: 2,
            previousRank: 4,
            previousTotal: 990,
            eventTotal: 50,
            transferCost: 0,
            new PickData(4, 4, 1, 2, 15, IsCaptain: true),
            new PickData(5, 5, 2, 1, 10, IsViceCaptain: true),
            new PickData(6, 6, 3, 1, 10));
        AddManager(
            fixture,
            303,
            "Gamma",
            "Cara",
            officialRank: 3,
            previousRank: 3,
            previousTotal: 970,
            eventTotal: 40,
            transferCost: 8,
            new PickData(7, 7, 1, 2, 10, IsCaptain: true),
            new PickData(8, 8, 2, 1, 10, IsViceCaptain: true),
            new PickData(9, 9, 3, 1, 10));
        var service = CreateService();

        // Act
        var result = Calculate(service, fixture);

        // Assert
        result.Managers.Select(manager => manager.EntryName)
            .Should().Equal("Beta", "Alpha", "Gamma");

        var beta = result.Managers[0];
        beta.OfficialRank.Should().Be(2);
        beta.PreviousRank.Should().Be(4);
        beta.PreviousTotalPoints.Should().Be(990);
        beta.OfficialTotalPoints.Should().Be(1040);
        beta.RawLiveGameweekPoints.Should().Be(50);
        beta.TransferCost.Should().Be(0);
        beta.LiveGameweekPoints.Should().Be(50);
        beta.LiveTotalPoints.Should().Be(1040);
        beta.LiveRank.Should().Be(1);
        beta.RankChange.Should().Be(3);
        beta.GapToLeader.Should().Be(0);

        var alpha = result.Managers[1];
        alpha.PreviousTotalPoints.Should().Be(1000);
        alpha.RawLiveGameweekPoints.Should().Be(30);
        alpha.LiveGameweekPoints.Should().Be(30);
        alpha.LiveTotalPoints.Should().Be(1030);
        alpha.LiveRank.Should().Be(2);
        alpha.RankChange.Should().Be(-1);
        alpha.GapToLeader.Should().Be(10);

        var gamma = result.Managers[2];
        gamma.RawLiveGameweekPoints.Should().Be(40);
        gamma.TransferCost.Should().Be(8);
        gamma.LiveGameweekPoints.Should().Be(32);
        gamma.LiveTotalPoints.Should().Be(1002);
        gamma.LiveRank.Should().Be(3);
        gamma.RankChange.Should().Be(0);
        gamma.GapToLeader.Should().Be(38);
    }

    [Test]
    public void Calculate_TransferHitChangesLiveOrder_UsesNetGameweekPoints()
    {
        // Arrange
        var fixture = new CalculationFixture();
        AddManager(
            fixture,
            101,
            "Alpha",
            "Alice",
            officialRank: 2,
            previousRank: 2,
            previousTotal: 1000,
            eventTotal: 0,
            transferCost: 0,
            new PickData(1, 1, 1, 2, 10, IsCaptain: true),
            new PickData(2, 2, 2, 1, 5, IsViceCaptain: true),
            new PickData(3, 3, 3, 1, 5));
        AddManager(
            fixture,
            202,
            "Beta",
            "Bob",
            officialRank: 1,
            previousRank: 1,
            previousTotal: 1000,
            eventTotal: 0,
            transferCost: 8,
            new PickData(4, 4, 1, 2, 10, IsCaptain: true),
            new PickData(5, 5, 2, 1, 10, IsViceCaptain: true),
            new PickData(6, 6, 3, 1, 5));

        // Act
        var result = Calculate(CreateService(), fixture);

        // Assert
        result.Managers.Select(manager => manager.EntryName)
            .Should().Equal("Alpha", "Beta");
        result.Managers[1].RawLiveGameweekPoints.Should().Be(35);
        result.Managers[1].TransferCost.Should().Be(8);
        result.Managers[1].LiveGameweekPoints.Should().Be(27);
        result.Managers[1].LiveTotalPoints.Should().Be(1027);
    }

    [Test]
    public void Calculate_TiedLiveTotals_AssignsCompetitionRanksAndStableOrdering()
    {
        // Arrange
        var fixture = new CalculationFixture();
        AddManager(
            fixture,
            1,
            "Zulu",
            "Zoe",
            officialRank: 2,
            previousRank: 2,
            previousTotal: 1000,
            eventTotal: 0,
            transferCost: 0,
            new PickData(1, 1, 1, 2, 40, IsCaptain: true),
            new PickData(2, 2, 2, 1, 10, IsViceCaptain: true),
            new PickData(3, 3, 3, 1, 10));
        AddManager(
            fixture,
            2,
            "Alpha",
            "Amy",
            officialRank: 2,
            previousRank: 2,
            previousTotal: 1000,
            eventTotal: 0,
            transferCost: 0,
            new PickData(4, 4, 1, 2, 40, IsCaptain: true),
            new PickData(5, 5, 2, 1, 10, IsViceCaptain: true),
            new PickData(6, 6, 3, 1, 10));
        AddManager(
            fixture,
            3,
            "Gamma",
            "Gina",
            officialRank: 4,
            previousRank: 4,
            previousTotal: 1000,
            eventTotal: 0,
            transferCost: 0,
            new PickData(7, 7, 1, 2, 35, IsCaptain: true),
            new PickData(8, 8, 2, 1, 10, IsViceCaptain: true),
            new PickData(9, 9, 3, 1, 10));
        AddManager(
            fixture,
            4,
            "Delta",
            "Dina",
            officialRank: 4,
            previousRank: 4,
            previousTotal: 1000,
            eventTotal: 0,
            transferCost: 0,
            new PickData(10, 10, 1, 2, 35, IsCaptain: true),
            new PickData(11, 11, 2, 1, 10, IsViceCaptain: true),
            new PickData(12, 12, 3, 1, 10));

        // Act
        var result = Calculate(CreateService(), fixture);

        // Assert
        result.Managers.Select(manager => manager.EntryName)
            .Should().Equal("Alpha", "Zulu", "Delta", "Gamma");
        result.Managers.Select(manager => manager.LiveRank)
            .Should().Equal(1, 1, 3, 3);
        result.Managers.Take(2).Select(manager => manager.GapToLeader)
            .Should().Equal(0, 0);
        result.Managers.Skip(2).Select(manager => manager.GapToLeader)
            .Should().Equal(10, 10);
    }

    [Test]
    public void Calculate_GameweekOne_DerivesPreviousTotalFromStandingTotals()
    {
        // Arrange
        var fixture = new CalculationFixture();
        AddManager(
            fixture,
            101,
            "Alpha",
            "Alice",
            officialRank: 1,
            previousRank: 0,
            previousTotal: 0,
            eventTotal: 25,
            transferCost: 0,
            new PickData(1, 1, 1, 2, 10, IsCaptain: true),
            new PickData(2, 2, 2, 1, 5, IsViceCaptain: true),
            new PickData(3, 3, 3, 1, 0));

        // Act
        var manager = Calculate(CreateService(), fixture).Managers.Single();

        // Assert
        manager.PreviousTotalPoints.Should().Be(0);
        manager.OfficialTotalPoints.Should().Be(25);
        manager.LiveGameweekPoints.Should().Be(25);
        manager.LiveTotalPoints.Should().Be(25);
        manager.RankChange.Should().Be(0);
    }

    [Test]
    public void Calculate_PlayerProgress_UsesFixtureStateAndEffectiveMultipliers()
    {
        // Arrange
        var fixture = new CalculationFixture();
        AddPlayer(fixture, 1, 1, "Finished Zero Minutes", 0, minutes: 0);
        AddPlayer(fixture, 2, 2, "Playing", 2);
        AddPlayer(fixture, 3, 3, "Yet To Play", 0);
        AddPlayer(fixture, 4, 4, "Bench", 5);
        AddPlayer(fixture, 5, 5, "Bench Boost", 5);
        fixture.Standings.Add(new ClassicStanding
        {
            Entry = 101,
            EntryName = "Alpha",
            PlayerName = "Alice",
            Rank = 1,
            LastRank = 1
        });
        fixture.PicksByEntry[101] = new EntryEventPicksResponse
        {
            EntryHistory = new EntryEventHistory(),
            ActiveChip = "bboost",
            Picks =
            [
                Pick(1, 1, 3, isCaptain: true),
                Pick(2, 2, 1, isViceCaptain: true),
                Pick(3, 3, 1),
                Pick(4, 12, 1),
                Pick(5, 13, 1)
            ]
        };
        fixture.Fixtures =
        [
            Fixture(1, 1, 101, started: true, finished: true),
            Fixture(2, 2, 102, started: true, finished: false),
            Fixture(3, 3, 103, started: false, finished: false),
            Fixture(4, 4, 104, started: true, finished: true),
            Fixture(5, 5, 105, started: false, finished: false)
        ];

        // Act
        var result = Calculate(CreateService(), fixture);

        // Assert
        result.Managers.Single().PlayerProgress.Should()
            .Be(new FplLivePlayerProgress(1, 2));
        result.Managers.Single().PlayerProgress.Active.Should().Be(3);
    }

    [Test]
    public void Calculate_ExistingAlerts_KeepBenchCaptainAndAutomaticSubstitutionInsights()
    {
        // Arrange
        var fixture = new CalculationFixture();
        AddManager(
            fixture,
            101,
            "Alpha",
            "Alice",
            officialRank: 1,
            previousRank: 1,
            previousTotal: 1000,
            eventTotal: 12,
            transferCost: 0,
            new PickData(1, 1, 1, 2, 1, IsCaptain: true),
            new PickData(2, 2, 2, 1, 10, IsViceCaptain: true),
            new PickData(4, 4, 3, 1, 9, Name: "Bench In"),
            new PickData(3, 3, 12, 0, 8, Name: "Bench One"),
            new PickData(5, 5, 13, 0, 0, Name: "Starter Out"));
        fixture.PicksByEntry[101].AutomaticSubstitutions.Add(
            new EntryAutomaticSubstitution
            {
                ElementIn = 4,
                ElementOut = 5
            });
        fixture.LivePlayers[5] = Stats(0, 0);

        // Act
        var result = Calculate(CreateService(), fixture);

        // Assert
        result.BenchAlerts.Select(manager => manager.EntryName)
            .Should().Equal("Alpha");
        result.CaptainDisasters.Select(manager => manager.EntryName)
            .Should().Equal("Alpha");
        result.Managers.Single().RawLiveGameweekPoints.Should().Be(21);
        result.Managers.Single().LiveTotalPoints.Should().Be(1021);
        result.AutomaticSubstitutionSalvations.Should().ContainSingle().Which.Should()
            .BeEquivalentTo(new FplAutomaticSubstitutionSalvation(
                101,
                "Alpha",
                "Bench In",
                9,
                "Starter Out",
                0,
                9));
    }

    [Test]
    public void Calculate_DoubleGameweek_RemainsActiveUntilEveryFixtureIsFinished()
    {
        // Arrange
        var fixture = CreateTwoPlayerFixture();
        fixture.Fixtures =
        [
            Fixture(1, 1, 101, started: true, finished: true),
            Fixture(2, 1, 102, started: false, finished: false),
            Fixture(3, 2, 103, started: true, finished: true)
        ];

        // Act
        var yetToPlay = Calculate(CreateService(), fixture);
        fixture.Fixtures[1] = Fixture(2, 1, 102, started: true, finished: false);
        var playing = Calculate(CreateService(), fixture);
        fixture.Fixtures[1] = Fixture(2, 1, 102, started: true, finished: true);
        var finished = Calculate(CreateService(), fixture);

        // Assert
        yetToPlay.Managers.Single().PlayerProgress.Should()
            .Be(new FplLivePlayerProgress(0, 1));
        playing.Managers.Single().PlayerProgress.Should()
            .Be(new FplLivePlayerProgress(1, 0));
        finished.Managers.Single().PlayerProgress.Should()
            .Be(new FplLivePlayerProgress(0, 0));
    }

    [Test]
    public void Calculate_PendingAutomaticSubstitution_ProjectsBenchPlayerBeforeFplUpdatesMultipliers()
    {
        // Arrange
        var fixture = CreateLegalAutosubFixture();
        var outgoingIndex = fixture.PicksByEntry[101].Picks.FindIndex(
            pick => pick.Element == 4);
        var incomingIndex = fixture.PicksByEntry[101].Picks.FindIndex(
            pick => pick.Element == 13);
        fixture.PicksByEntry[101].Picks[outgoingIndex] = Pick(4, 4, 1);
        fixture.PicksByEntry[101].Picks[incomingIndex] = Pick(13, 13, 0);
        fixture.LivePlayers[4] = Stats(0, 0);
        fixture.LivePlayers[13] = Stats(9, 90);

        // Act
        var manager = Calculate(CreateService(), fixture).Managers.Single();

        // Assert
        manager.RawLiveGameweekPoints.Should().Be(9);
        manager.BenchPoints.Should().Be(0);
        manager.AutomaticSubstitutionSalvations.Should().ContainSingle().Which.SavedPoints
            .Should().Be(9);
    }

    [Test]
    public void Calculate_ZeroMinuteCardedBenchPlayer_CanBeProjectedAsAutomaticSubstitution()
    {
        // Arrange
        var fixture = CreateLegalAutosubFixture();
        var outgoingIndex = fixture.PicksByEntry[101].Picks.FindIndex(
            pick => pick.Element == 3);
        var incomingIndex = fixture.PicksByEntry[101].Picks.FindIndex(
            pick => pick.Element == 13);
        fixture.PicksByEntry[101].Picks[outgoingIndex] = Pick(3, 3, 1);
        fixture.PicksByEntry[101].Picks[incomingIndex] = Pick(13, 13, 0);
        fixture.LivePlayers[3] = Stats(0, 0);
        fixture.LivePlayers[13] = Stats(9, 0, yellowCards: 1);

        // Act
        var manager = Calculate(CreateService(), fixture).Managers.Single();

        // Assert
        manager.RawLiveGameweekPoints.Should().Be(9);
        manager.BenchPoints.Should().Be(0);
        manager.AutomaticSubstitutionSalvations.Should().ContainSingle().Which.SavedPoints
            .Should().Be(9);
    }

    [Test]
    public void Calculate_CaptainDidNotPlay_TransfersCaptainMultiplierToPlayingViceCaptain()
    {
        // Arrange
        var fixture = CreateTwoPlayerFixture();
        fixture.PicksByEntry[101].Picks[0] = Pick(1, 1, 0, isCaptain: true);
        fixture.PicksByEntry[101].Picks[1] = Pick(2, 2, 1, isViceCaptain: true);
        fixture.LivePlayers[1] = Stats(0, 0);
        fixture.LivePlayers[2] = Stats(10, 90);

        // Act
        var manager = Calculate(CreateService(), fixture).Managers.Single();

        // Assert
        manager.RawLiveGameweekPoints.Should().Be(20);
        manager.Captain.CaptainEffectivePoints.Should().Be(0);
        manager.Captain.ViceCaptainEffectivePoints.Should().Be(20);
    }

    [Test]
    public void Calculate_ZeroMinuteCardedCaptain_RetainsCaptaincy()
    {
        // Arrange
        var fixture = CreateTwoPlayerFixture();
        fixture.PicksByEntry[101].Picks[0] = Pick(1, 1, 0, isCaptain: true);
        fixture.PicksByEntry[101].Picks[1] = Pick(2, 2, 1, isViceCaptain: true);
        fixture.LivePlayers[1] = Stats(-1, 0, yellowCards: 1);
        fixture.LivePlayers[2] = Stats(10, 90);

        // Act
        var manager = Calculate(CreateService(), fixture).Managers.Single();

        // Assert
        manager.RawLiveGameweekPoints.Should().Be(8);
        manager.Captain.CaptainEffectivePoints.Should().Be(-2);
        manager.Captain.ViceCaptainEffectivePoints.Should().Be(10);
    }

    [Test]
    public void Calculate_TripleCaptainDidNotPlay_TransfersTripleMultiplierToPlayingViceCaptain()
    {
        // Arrange
        var fixture = CreateTwoPlayerFixture();
        var picks = fixture.PicksByEntry[101];
        fixture.PicksByEntry[101] = new EntryEventPicksResponse
        {
            ActiveChip = "3xc",
            EntryHistory = picks.EntryHistory,
            Picks = picks.Picks,
            AutomaticSubstitutions = picks.AutomaticSubstitutions
        };
        fixture.PicksByEntry[101].Picks[0] = Pick(1, 1, 0, isCaptain: true);
        fixture.PicksByEntry[101].Picks[1] = Pick(2, 2, 1, isViceCaptain: true);
        fixture.LivePlayers[1] = Stats(0, 0);
        fixture.LivePlayers[2] = Stats(10, 90);

        // Act
        var manager = Calculate(CreateService(), fixture).Managers.Single();

        // Assert
        manager.RawLiveGameweekPoints.Should().Be(30);
        manager.Captain.CaptainEffectivePoints.Should().Be(0);
        manager.Captain.ViceCaptainEffectivePoints.Should().Be(30);
    }

    [Test]
    public void Calculate_BenchBoost_DoesNotReportAutomaticSubstitutionSalvation()
    {
        // Arrange
        var fixture = CreateLegalAutosubFixture(benchBoost: true);
        var outgoingIndex = fixture.PicksByEntry[101].Picks.FindIndex(
            pick => pick.Element == 4);
        var incomingIndex = fixture.PicksByEntry[101].Picks.FindIndex(
            pick => pick.Element == 13);
        fixture.PicksByEntry[101].Picks[outgoingIndex] = Pick(4, 4, 0);
        fixture.PicksByEntry[101].Picks[incomingIndex] = Pick(13, 13, 1);
        fixture.PicksByEntry[101].AutomaticSubstitutions.Add(
            new EntryAutomaticSubstitution
            {
                ElementIn = 13,
                ElementOut = 4
            });
        fixture.LivePlayers[4] = Stats(0, 0);
        fixture.LivePlayers[13] = Stats(9, 90);

        // Act
        var manager = Calculate(CreateService(), fixture).Managers.Single();

        // Assert
        manager.RawLiveGameweekPoints.Should().Be(9);
        manager.BenchPoints.Should().Be(0);
        manager.AutomaticSubstitutionSalvations.Should().BeEmpty();
    }

    [Test]
    public void Calculate_CaptainFixtureNotFinished_DoesNotDeclarePrematureCaptainDisaster()
    {
        // Arrange
        var fixture = CreateTwoPlayerFixture();
        fixture.Fixtures[0] = Fixture(1, 1, 101, started: false, finished: false);
        fixture.PicksByEntry[101].Picks[0] = Pick(1, 1, 2, isCaptain: true);
        fixture.PicksByEntry[101].Picks[1] = Pick(2, 2, 1, isViceCaptain: true);
        fixture.LivePlayers[1] = Stats(0, 0);
        fixture.LivePlayers[2] = Stats(10, 90);

        // Act
        var result = Calculate(CreateService(), fixture);

        // Assert
        result.CaptainDisasters.Should().BeEmpty();
    }

    [Test]
    public void Calculate_RemainingPlayers_ProducesCaptainClashAndUniquePlayerInsights()
    {
        // Arrange
        var fixture = new CalculationFixture();
        AddManager(
            fixture,
            101,
            "Alpha",
            "Alice",
            officialRank: 1,
            previousRank: 1,
            previousTotal: 1000,
            eventTotal: 0,
            transferCost: 0,
            new PickData(1, 1, 1, 2, 5, IsCaptain: true, Name: "Salah"),
            new PickData(2, 10, 2, 1, 0, IsViceCaptain: true, Name: "Shared"),
            new PickData(3, 3, 3, 1, 0, Name: "Alpha Unique"));
        AddManager(
            fixture,
            202,
            "Beta",
            "Bob",
            officialRank: 2,
            previousRank: 2,
            previousTotal: 990,
            eventTotal: 0,
            transferCost: 0,
            new PickData(4, 4, 1, 2, 5, IsCaptain: true, Name: "Haaland"),
            new PickData(2, 10, 2, 1, 0, IsViceCaptain: true, Name: "Shared"),
            new PickData(5, 5, 3, 1, 0, Name: "Beta Finished"));
        AddManager(
            fixture,
            303,
            "Gamma",
            "Cara",
            officialRank: 3,
            previousRank: 3,
            previousTotal: 980,
            eventTotal: 0,
            transferCost: 0,
            new PickData(6, 6, 1, 2, 5, IsCaptain: true, Name: "Gamma Captain"),
            new PickData(2, 10, 2, 1, 0, IsViceCaptain: true, Name: "Shared"),
            new PickData(7, 7, 3, 1, 0, Name: "Gamma Finished"));
        fixture.Fixtures =
        [
            Fixture(1, 1, 101, started: false, finished: false),
            Fixture(2, 10, 102, started: true, finished: true),
            Fixture(3, 3, 103, started: false, finished: false),
            Fixture(4, 4, 104, started: false, finished: false),
            Fixture(5, 5, 105, started: true, finished: true),
            Fixture(6, 6, 106, started: true, finished: true),
            Fixture(7, 7, 107, started: true, finished: true)
        ];

        // Act
        var result = Calculate(CreateService(), fixture);

        // Assert
        result.SwingInsights[0].Should().Be(new CaptainClashInsight(
            101,
            "Alpha",
            "Salah",
            202,
            "Beta",
            "Haaland"));
        result.SwingInsights.OfType<UniqueRemainingPlayerInsight>()
            .Select(insight => (insight.EntryName, insight.PlayerName))
            .Should().Contain(("Alpha", "Salah"));
        result.SwingInsights.OfType<UniqueRemainingPlayerInsight>()
            .Select(insight => (insight.EntryName, insight.PlayerName))
            .Should().Contain(("Alpha", "Alpha Unique"));
    }

    [Test]
    public void Calculate_MissingPlayerLiveData_RejectsIncompleteSource()
    {
        // Arrange
        var fixture = CreateCoreFixture();
        fixture.LivePlayers.Remove(1);

        // Act
        var act = () => Calculate(CreateService(), fixture);

        // Assert
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*live response*player 1*");
    }

    [Test]
    public void Calculate_MissingEntryHistory_RejectsIncompleteSource()
    {
        // Arrange
        var fixture = CreateCoreFixture();
        fixture.PicksByEntry[101] = new EntryEventPicksResponse
        {
            Picks = fixture.PicksByEntry[101].Picks,
            EntryHistory = null
        };

        // Act
        var act = () => Calculate(CreateService(), fixture);

        // Assert
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*entry history*entry 101*");
    }

    [Test]
    public void Calculate_StandingTotalBelowEventTotal_RejectsIncompleteSource()
    {
        // Arrange
        var fixture = CreateCoreFixture();
        fixture.Standings[0].Total = 10;
        fixture.Standings[0].EventTotal = 30;

        // Act
        var act = () => Calculate(CreateService(), fixture);

        // Assert
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*standings response*manager data*");
    }

    [Test]
    public void Calculate_MissingFixtureState_RejectsIncompleteFixtureSource()
    {
        // Arrange
        var fixture = CreateCoreFixture();
        fixture.Fixtures[0] = new PremierLeagueFixture
        {
            Id = 1,
            EventId = 5,
            HomeTeamId = 1,
            AwayTeamId = 1001,
            Started = null,
            Finished = false
        };

        // Act
        var act = () => Calculate(CreateService(), fixture);

        // Assert
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*fixtures response*incomplete*");
    }

    [Test]
    public void Calculate_UnmatchedPlayerTeam_RejectsIncompleteFixtureSource()
    {
        // Arrange
        var fixture = CreateCoreFixture();
        fixture.Fixtures.RemoveAll(item => item.HomeTeamId == 1 || item.AwayTeamId == 1);

        // Act
        var act = () => Calculate(CreateService(), fixture);

        // Assert
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*fixtures response*team 1*");
    }

    private static FplLiveInsightsCalculationService CreateService()
    {
        return new FplLiveInsightsCalculationService(
            new FantasyPremierLeagueOptions
            {
                LargeBenchPointsThreshold = 8,
                CaptainSuccessEffectivePointsThreshold = 20,
                CaptainDisasterPointsThreshold = 2,
                CaptainDisasterViceCaptainPointsThreshold = 8
            });
    }

    private static FplLiveGameweek Calculate(
        FplLiveInsightsCalculationService service,
        CalculationFixture fixture)
    {
        return service.Calculate(
            "2026/27",
            5,
            SourceUpdatedAt,
            CapturedAt,
            fixture.Standings,
            fixture.PicksByEntry,
            fixture.Players,
            fixture.LivePlayers,
            fixture.Fixtures);
    }

    private static CalculationFixture CreateCoreFixture()
    {
        var fixture = new CalculationFixture();
        AddManager(
            fixture,
            101,
            "Alpha",
            "Alice",
            officialRank: 1,
            previousRank: 1,
            previousTotal: 1000,
            eventTotal: 30,
            transferCost: 0,
            new PickData(1, 1, 1, 2, 10, IsCaptain: true),
            new PickData(2, 2, 2, 1, 5, IsViceCaptain: true),
            new PickData(3, 3, 3, 1, 5));
        AddManager(
            fixture,
            202,
            "Beta",
            "Bob",
            officialRank: 2,
            previousRank: 2,
            previousTotal: 990,
            eventTotal: 50,
            transferCost: 0,
            new PickData(4, 4, 1, 2, 15, IsCaptain: true),
            new PickData(5, 5, 2, 1, 10, IsViceCaptain: true),
            new PickData(6, 6, 3, 1, 10));
        AddManager(
            fixture,
            303,
            "Gamma",
            "Cara",
            officialRank: 3,
            previousRank: 3,
            previousTotal: 970,
            eventTotal: 40,
            transferCost: 8,
            new PickData(7, 7, 1, 2, 10, IsCaptain: true),
            new PickData(8, 8, 2, 1, 10, IsViceCaptain: true),
            new PickData(9, 9, 3, 1, 10));
        return fixture;
    }

    private static CalculationFixture CreateTwoPlayerFixture()
    {
        var fixture = new CalculationFixture();
        AddManager(
            fixture,
            101,
            "Alpha",
            "Alice",
            officialRank: 1,
            previousRank: 1,
            previousTotal: 100,
            eventTotal: 0,
            transferCost: 0,
            new PickData(1, 1, 1, 2, 5, IsCaptain: true),
            new PickData(2, 2, 2, 1, 0, IsViceCaptain: true));
        return fixture;
    }

    private static CalculationFixture CreateLegalAutosubFixture(bool benchBoost = false)
    {
        var fixture = new CalculationFixture();
        AddManager(
            fixture,
            101,
            "Alpha",
            "Alice",
            officialRank: 1,
            previousRank: 1,
            previousTotal: 1000,
            eventTotal: 0,
            transferCost: 0,
            new PickData(1, 1, 1, 2, 0, ElementType: 1, IsCaptain: true),
            new PickData(2, 2, 2, 1, 0, ElementType: 2, IsViceCaptain: true),
            new PickData(3, 3, 3, 1, 0, ElementType: 2),
            new PickData(4, 4, 4, 1, 0, ElementType: 2),
            new PickData(5, 5, 5, 1, 0, ElementType: 3),
            new PickData(6, 6, 6, 1, 0, ElementType: 3),
            new PickData(7, 7, 7, 1, 0, ElementType: 3),
            new PickData(8, 8, 8, 1, 0, ElementType: 3),
            new PickData(9, 9, 9, 1, 0, ElementType: 4),
            new PickData(10, 10, 10, 1, 0, ElementType: 4),
            new PickData(11, 11, 11, 1, 0, ElementType: 4),
            new PickData(12, 12, 12, 0, 0, ElementType: 1),
            new PickData(13, 13, 13, 0, 9, ElementType: 2),
            new PickData(14, 14, 14, 0, 0, ElementType: 3),
            new PickData(15, 15, 15, 0, 0, ElementType: 4));
        if (benchBoost)
        {
            var picks = fixture.PicksByEntry[101];
            fixture.PicksByEntry[101] = new EntryEventPicksResponse
            {
                ActiveChip = "bboost",
                EntryHistory = picks.EntryHistory,
                Picks = picks.Picks,
                AutomaticSubstitutions = picks.AutomaticSubstitutions
            };
        }

        return fixture;
    }

    private static void AddManager(
        CalculationFixture fixture,
        int entryId,
        string entryName,
        string managerName,
        int officialRank,
        int previousRank,
        int previousTotal,
        int eventTotal,
        int transferCost,
        params PickData[] pickData)
    {
        fixture.Standings.Add(new ClassicStanding
        {
            Entry = entryId,
            EntryName = entryName,
            PlayerName = managerName,
            Rank = officialRank,
            LastRank = previousRank,
            Total = checked(previousTotal + eventTotal),
            EventTotal = eventTotal
        });
        fixture.PicksByEntry[entryId] = new EntryEventPicksResponse
        {
            EntryHistory = new EntryEventHistory
            {
                EventTransfersCost = transferCost
            },
            Picks = pickData.Select(data => Pick(
                data.PlayerId,
                data.Position,
                data.Multiplier,
                data.IsCaptain,
                data.IsViceCaptain)).ToList()
        };

        foreach (var data in pickData)
        {
            AddPlayer(fixture, data.PlayerId, data.TeamId, data.Name ?? $"Player {data.PlayerId}",
                data.Points,
                elementType: data.ElementType);
        }
    }

    private static void AddPlayer(
        CalculationFixture fixture,
        int playerId,
        int teamId,
        string name,
        int points,
        int minutes = 90,
        int elementType = 2)
    {
        fixture.Players[playerId] = new PremierLeagueElement
        {
            Id = playerId,
            TeamId = teamId,
            ElementType = elementType,
            WebName = name
        };
        fixture.LivePlayers[playerId] = Stats(points, minutes);
        if (fixture.Fixtures.All(item =>
                item.HomeTeamId != teamId && item.AwayTeamId != teamId))
        {
            fixture.Fixtures.Add(Fixture(
                fixture.Fixtures.Count + 1,
                teamId,
                teamId + 1000,
                started: true,
                finished: true));
        }
    }

    private static EntryEventPick Pick(
        int element,
        int position,
        int multiplier,
        bool isCaptain = false,
        bool isViceCaptain = false)
    {
        return new EntryEventPick
        {
            Element = element,
            Position = position,
            Multiplier = multiplier,
            IsCaptain = isCaptain,
            IsViceCaptain = isViceCaptain
        };
    }

    private static EventLiveElementStats Stats(
        int totalPoints,
        int minutes,
        int yellowCards = 0,
        int redCards = 0)
    {
        return new EventLiveElementStats
        {
            TotalPoints = totalPoints,
            Minutes = minutes,
            YellowCards = yellowCards,
            RedCards = redCards
        };
    }

    private static PremierLeagueFixture Fixture(
        int id,
        int homeTeamId,
        int awayTeamId,
        bool started,
        bool finished)
    {
        return new PremierLeagueFixture
        {
            Id = id,
            EventId = 5,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Started = started,
            Finished = finished
        };
    }

    private sealed record PickData(
        int PlayerId,
        int TeamId,
        int Position,
        int Multiplier,
        int Points,
        int ElementType = 2,
        bool IsCaptain = false,
        bool IsViceCaptain = false,
        string? Name = null);

    private sealed class CalculationFixture
    {
        public List<ClassicStanding> Standings { get; } = [];

        public Dictionary<int, EntryEventPicksResponse> PicksByEntry { get; } = [];

        public Dictionary<int, PremierLeagueElement> Players { get; } = [];

        public Dictionary<int, EventLiveElementStats> LivePlayers { get; } = [];

        public List<PremierLeagueFixture> Fixtures { get; set; } = [];
    }
}
