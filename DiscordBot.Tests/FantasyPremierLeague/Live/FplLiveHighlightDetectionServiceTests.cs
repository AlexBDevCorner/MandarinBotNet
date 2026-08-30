using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Live;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Live;

[TestFixture]
public sealed class FplLiveHighlightDetectionServiceTests
{
    private FplLiveHighlightDetectionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new FplLiveHighlightDetectionService(
            new FantasyPremierLeagueOptions());
    }

    [Test]
    public void Detect_IdenticalState_ReturnsNoHighlights()
    {
        // Arrange
        var gameweek = FplLiveTestData.CreateGameweek();

        // Act
        var highlights = _service.Detect(gameweek, gameweek);

        // Assert
        highlights.Should().BeEmpty();
    }

    [Test]
    public void Detect_OrdinaryPointIncrease_ReturnsNoHighlights()
    {
        // Arrange
        var previous = FplLiveTestData.CreateGameweek();
        var managers = previous.Managers.ToArray();
        managers[1] = managers[1] with
        {
            LiveGameweekPoints = managers[1].LiveGameweekPoints + 1,
            LiveTotalPoints = managers[1].LiveTotalPoints + 1
        };
        var current = FplLiveTestData.CreateGameweek(managers);

        // Act
        var highlights = _service.Detect(previous, current);

        // Assert
        highlights.Should().BeEmpty();
    }

    [Test]
    public void Detect_PlayerStartsPlaying_ReturnsNoHighlights()
    {
        // Arrange
        var previousManagers = FplLiveTestData.CreateGameweek().Managers.ToArray();
        previousManagers[0] = previousManagers[0] with
        {
            PlayerProgress = new FplLivePlayerProgress(0, 1)
        };
        var currentManagers = previousManagers.ToArray();
        currentManagers[0] = currentManagers[0] with
        {
            PlayerProgress = new FplLivePlayerProgress(1, 0)
        };

        // Act
        var highlights = _service.Detect(
            FplLiveTestData.CreateGameweek(previousManagers),
            FplLiveTestData.CreateGameweek(currentManagers));

        // Assert
        highlights.Should().BeEmpty();
    }

    [Test]
    public void Detect_OnePlaceRankChange_ReturnsNoHighlights()
    {
        // Arrange
        var previous = FplLiveTestData.CreateGameweek();
        var currentManagers = previous.Managers.ToArray();
        currentManagers[1] = currentManagers[1] with { LiveRank = 3 };
        currentManagers[2] = currentManagers[2] with { LiveRank = 2 };

        // Act
        var highlights = _service.Detect(
            previous,
            FplLiveTestData.CreateGameweek(currentManagers));

        // Assert
        highlights.Should().BeEmpty();
    }

    [Test]
    public void Detect_TwoPlaceRankChange_ReturnsRankHighlight()
    {
        // Arrange
        var previous = FplLiveTestData.CreateGameweek();
        var currentManagers = previous.Managers.ToArray();
        currentManagers[1] = currentManagers[1] with { LiveRank = 4 };
        currentManagers[2] = currentManagers[2] with { LiveRank = 2 };
        currentManagers[3] = currentManagers[3] with { LiveRank = 3 };

        // Act
        var highlights = _service.Detect(
            previous,
            FplLiveTestData.CreateGameweek(currentManagers));

        // Assert
        highlights.Should().ContainSingle();
        highlights[0].Should().BeOfType<SignificantRankChangeHighlight>()
            .Which.EntryId.Should().Be(2);
    }

    [Test]
    public void Detect_LeaderChanges_ReturnsLeaderHighlight()
    {
        // Arrange
        var previous = FplLiveTestData.CreateGameweek();
        var currentManagers = previous.Managers.ToArray();
        currentManagers[0] = currentManagers[0] with { LiveRank = 2 };
        currentManagers[1] = currentManagers[1] with
        {
            LiveRank = 1,
            LiveTotalPoints = 123
        };

        // Act
        var highlights = _service.Detect(
            previous,
            FplLiveTestData.CreateGameweek(currentManagers));

        // Assert
        highlights.Should().ContainSingle();
        highlights[0].Should().BeOfType<LeaderChangedHighlight>()
            .Which.NewLeaderEntryId.Should().Be(2);
    }

    [Test]
    public void Detect_LeaderChanges_DoesNotReturnRedundantRankHighlights()
    {
        // Arrange
        var service = new FplLiveHighlightDetectionService(
            new FantasyPremierLeagueOptions { SignificantLiveRankChange = 1 });
        var previous = FplLiveTestData.CreateGameweek();
        var currentManagers = previous.Managers.ToArray();
        currentManagers[0] = currentManagers[0] with { LiveRank = 2 };
        currentManagers[1] = currentManagers[1] with { LiveRank = 1 };

        // Act
        var highlights = service.Detect(
            previous,
            FplLiveTestData.CreateGameweek(currentManagers));

        // Assert
        highlights.Should().ContainSingle()
            .Which.Should().BeOfType<LeaderChangedHighlight>();
    }

    [Test]
    public void Detect_BenchThresholdCrossed_ReturnsBenchHighlight()
    {
        // Arrange
        var previous = CreateWithFirstManager(benchPoints: 7);
        var current = CreateWithFirstManager(benchPoints: 8);

        // Act
        var highlights = _service.Detect(previous, current);

        // Assert
        highlights.Should().ContainSingle()
            .Which.Should().BeOfType<BenchThresholdReachedHighlight>();
    }

    [Test]
    public void Detect_BenchAlreadyAboveThreshold_ReturnsNoHighlight()
    {
        // Arrange
        var previous = CreateWithFirstManager(benchPoints: 8);
        var current = CreateWithFirstManager(benchPoints: 9);

        // Act
        var highlights = _service.Detect(previous, current);

        // Assert
        highlights.Should().BeEmpty();
    }

    [Test]
    public void Detect_CaptainSuccessThresholdCrossed_ReturnsCaptainHighlight()
    {
        // Arrange
        var previous = CreateWithFirstManager(captainEffectivePoints: 18);
        var current = CreateWithFirstManager(captainEffectivePoints: 20);

        // Act
        var highlights = _service.Detect(previous, current);

        // Assert
        highlights.Should().ContainSingle()
            .Which.Should().BeOfType<CaptainSuccessHighlight>();
    }

    [Test]
    public void Detect_CaptainAlreadySuccessful_ReturnsNoHighlight()
    {
        // Arrange
        var previous = CreateWithFirstManager(captainEffectivePoints: 20);
        var current = CreateWithFirstManager(captainEffectivePoints: 22);

        // Act
        var highlights = _service.Detect(previous, current);

        // Assert
        highlights.Should().BeEmpty();
    }

    [Test]
    public void Detect_NewCaptainDisaster_ReturnsDisasterHighlight()
    {
        // Arrange
        var previous = FplLiveTestData.CreateGameweek();
        var current = FplLiveTestData.CreateGameweek(captainDisasterEntryIds: [1]);

        // Act
        var highlights = _service.Detect(previous, current);

        // Assert
        highlights.Should().ContainSingle()
            .Which.Should().BeOfType<CaptainDisasterHighlight>();
    }

    [Test]
    public void Detect_ExistingCaptainDisaster_ReturnsNoHighlight()
    {
        // Arrange
        var previous = FplLiveTestData.CreateGameweek(captainDisasterEntryIds: [1]);
        var current = FplLiveTestData.CreateGameweek(captainDisasterEntryIds: [1]);

        // Act
        var highlights = _service.Detect(previous, current);

        // Assert
        highlights.Should().BeEmpty();
    }

    [Test]
    public void Detect_AutomaticSubstitutionBelowThreshold_ReturnsNoHighlight()
    {
        // Arrange
        var previous = FplLiveTestData.CreateGameweek();
        var current = FplLiveTestData.CreateGameweek(
            substitutions: [FplLiveTestData.CreateSubstitution(4)]);

        // Act
        var highlights = _service.Detect(previous, current);

        // Assert
        highlights.Should().BeEmpty();
    }

    [Test]
    public void Detect_SignificantAutomaticSubstitution_ReturnsSubstitutionHighlight()
    {
        // Arrange
        var previous = FplLiveTestData.CreateGameweek();
        var current = FplLiveTestData.CreateGameweek(
            substitutions: [FplLiveTestData.CreateSubstitution(5)]);

        // Act
        var highlights = _service.Detect(previous, current);

        // Assert
        highlights.Should().ContainSingle()
            .Which.Should().BeOfType<AutomaticSubstitutionHighlight>();
    }

    private static FplLiveGameweek CreateWithFirstManager(
        int benchPoints = 0,
        int captainEffectivePoints = 0)
    {
        var managers = FplLiveTestData.CreateGameweek().Managers.ToArray();
        managers[0] = managers[0] with
        {
            BenchPoints = benchPoints,
            Captain = managers[0].Captain with
            {
                CaptainEffectivePoints = captainEffectivePoints
            }
        };
        return FplLiveTestData.CreateGameweek(managers);
    }
}
