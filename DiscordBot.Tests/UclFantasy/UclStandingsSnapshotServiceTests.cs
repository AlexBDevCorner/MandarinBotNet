using AwesomeAssertions;
using DiscordBot.UclFantasy;
using NUnit.Framework;

namespace DiscordBot.Tests.UclFantasy;

[TestFixture]
public sealed class UclStandingsSnapshotServiceTests
{
    private UclStandingsSnapshotService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new UclStandingsSnapshotService();
    }

    [Test]
    public void ComputeFingerprint_SameMembersDifferentOrder_ProducesSameFingerprint()
    {
        // Arrange
        var ordered = SnapshotA();
        var reversed = SnapshotA();
        reversed.Members.ToList().Reverse();

        // Act
        var first = _service.ComputeFingerprint(ordered);
        var second = _service.ComputeFingerprint(
            ordered with { Members = [.. ordered.Members.Reverse()] });

        // Assert
        second.Should().Be(first);
    }

    [Test]
    public void ComputeFingerprint_IdenticalSnapshotTwice_IsDeterministic()
    {
        // Arrange
        var snapshot = SnapshotA();

        // Act
        var first = _service.ComputeFingerprint(snapshot);
        var second = new UclStandingsSnapshotService().ComputeFingerprint(SnapshotA());

        // Assert
        second.Should().Be(first);
        first.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void ComputeFingerprint_LeagueNameOnlyDifference_ProducesSameFingerprint()
    {
        // Arrange: decorative league naming must not trigger notifications.
        var before = SnapshotA();
        var after = SnapshotA() with { LeagueName = "Renamed League" };

        // Act
        var first = _service.ComputeFingerprint(before);
        var second = _service.ComputeFingerprint(after);

        // Assert
        second.Should().Be(first);
    }

    [Test]
    public void ComputeFingerprint_RankChange_ProducesDifferentFingerprint()
    {
        // Arrange
        var before = SnapshotA();
        var after = SnapshotA();
        var members = after.Members.ToList();
        members[0] = members[0] with { Rank = 2, LastRank = 1 };
        members[1] = members[1] with { Rank = 1, LastRank = 2 };
        after = after with { Members = members };

        // Act
        var first = _service.ComputeFingerprint(before);
        var second = _service.ComputeFingerprint(after);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_TotalPointsChange_ProducesDifferentFingerprint()
    {
        // Arrange
        var before = SnapshotA();
        var after = SnapshotA();
        var members = after.Members.ToList();
        members[0] = members[0] with { TotalPoints = members[0].TotalPoints + 1 };
        after = after with { Members = members };

        // Act
        var first = _service.ComputeFingerprint(before);
        var second = _service.ComputeFingerprint(after);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_MatchdayPointsChange_ProducesDifferentFingerprint()
    {
        // Arrange
        var before = SnapshotA();
        var after = SnapshotA();
        var members = after.Members.ToList();
        members[2] = members[2] with { MatchdayPoints = members[2].MatchdayPoints + 5 };
        after = after with { Members = members };

        // Act
        var first = _service.ComputeFingerprint(before);
        var second = _service.ComputeFingerprint(after);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_NonPodiumChange_ProducesDifferentFingerprint()
    {
        // Arrange: a change outside the top three must still change the hash.
        var before = SnapshotA();
        var after = SnapshotA();
        var members = after.Members.ToList();
        members[^1] = members[^1] with { TotalPoints = members[^1].TotalPoints + 10 };
        after = after with { Members = members };

        // Act
        var first = _service.ComputeFingerprint(before);
        var second = _service.ComputeFingerprint(after);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_EntrantAddedOrRemoved_ProducesDifferentFingerprint()
    {
        // Arrange
        var before = SnapshotA();
        var added = SnapshotA();
        var addedMembers = added.Members.ToList();
        addedMembers.Add(new UclLeagueStanding(999, "Newcomer", "Nina", 5, 5, 100, 20));
        added = added with { Members = addedMembers };
        var removed = SnapshotA() with { Members = SnapshotA().Members.Take(2).ToList() };

        // Act
        var baseline = _service.ComputeFingerprint(before);
        var withAdded = _service.ComputeFingerprint(added);
        var withRemoved = _service.ComputeFingerprint(removed);

        // Assert
        withAdded.Should().NotBe(baseline);
        withRemoved.Should().NotBe(baseline);
    }

    [Test]
    public void ComputeFingerprint_TeamNameChange_ProducesDifferentFingerprint()
    {
        // Arrange
        var before = SnapshotA();
        var after = SnapshotA();
        var members = after.Members.ToList();
        members[0] = members[0] with { TeamName = members[0].TeamName + " II" };
        after = after with { Members = members };

        // Act
        var first = _service.ComputeFingerprint(before);
        var second = _service.ComputeFingerprint(after);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_ManagerNameChange_ProducesDifferentFingerprint()
    {
        // Arrange
        var before = SnapshotA();
        var after = SnapshotA();
        var members = after.Members.ToList();
        members[0] = members[0] with { ManagerName = "Someone Else" };
        after = after with { Members = members };

        // Act
        var first = _service.ComputeFingerprint(before);
        var second = _service.ComputeFingerprint(after);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_MatchdayIdentityChange_ProducesDifferentFingerprint()
    {
        // Arrange
        var before = SnapshotA();
        var afterPhase = before with { PhaseId = before.PhaseId + 1 };
        var afterMatchday = before with { MatchdayId = before.MatchdayId + 1 };

        // Act
        var baseline = _service.ComputeFingerprint(before);

        // Assert
        _service.ComputeFingerprint(afterPhase).Should().NotBe(baseline);
        _service.ComputeFingerprint(afterMatchday).Should().NotBe(baseline);
    }

    [Test]
    public void ComputeFingerprint_DifferentLeagueId_ProducesDifferentFingerprint()
    {
        // Arrange
        var firstLeague = SnapshotA();
        var secondLeague = SnapshotA() with { LeagueId = 11111111 };

        // Act
        var first = _service.ComputeFingerprint(firstLeague);
        var second = _service.ComputeFingerprint(secondLeague);

        // Assert
        second.Should().NotBe(first);
    }

    private static UclLeagueStandings SnapshotA()
    {
        return new UclLeagueStandings(
            25247707,
            "Great Messi League",
            1,
            2,
            [
                new UclLeagueStanding(101, "Alpha FC", "Anna", 1, 1, 200, 60),
                new UclLeagueStanding(102, "Beta United", "Boris", 2, 2, 190, 55),
                new UclLeagueStanding(103, "Gamma City", "Gita", 3, 3, 180, 40),
                new UclLeagueStanding(104, "Delta Rovers", "Dana", 4, 5, 170, 70)
            ]);
    }
}
