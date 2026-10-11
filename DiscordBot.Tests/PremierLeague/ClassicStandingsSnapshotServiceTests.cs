using AwesomeAssertions;
using DiscordBot.PremierLeague;
using DiscordBot.Responses;
using NUnit.Framework;

namespace DiscordBot.Tests.PremierLeague;

[TestFixture]
public sealed class ClassicStandingsSnapshotServiceTests
{
    private ClassicStandingsSnapshotService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new ClassicStandingsSnapshotService();
    }

    [Test]
    public void ComputeFingerprint_SamePodiumDifferentOrder_ProducesSameFingerprint()
    {
        // Arrange
        var ordered = OctSnapshot();
        var reversed = OctSnapshot().AsEnumerable().Reverse().ToArray();

        // Act
        var first = _service.ComputeFingerprint(123, ordered);
        var second = _service.ComputeFingerprint(123, reversed);

        // Assert
        second.Should().Be(first);
    }

    [Test]
    public void ComputeFingerprint_IdenticalSnapshotTwice_IsDeterministic()
    {
        // Arrange
        var snapshot = OctSnapshot();

        // Act
        var first = _service.ComputeFingerprint(123, snapshot);
        var second = new ClassicStandingsSnapshotService().ComputeFingerprint(123, OctSnapshot());

        // Assert
        second.Should().Be(first);
        first.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void ComputeFingerprint_TimestampOnlyDifference_IsNotRepresented()
    {
        // Arrange: the service takes only structured results; timestamps are
        // excluded by construction.
        var snapshot = OctSnapshot();

        // Act
        var first = _service.ComputeFingerprint(123, snapshot);
        var second = _service.ComputeFingerprint(123, OctSnapshot());

        // Assert
        second.Should().Be(first);
    }

    [Test]
    public void ComputeFingerprint_RankChange_ProducesDifferentFingerprint()
    {
        // Arrange
        var before = OctSnapshot();
        var after = OctSnapshot();
        after[1].Rank = 3;
        after[2].Rank = 2;

        // Act
        var first = _service.ComputeFingerprint(123, before);
        var second = _service.ComputeFingerprint(123, after);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_TotalPointsChange_ProducesDifferentFingerprint()
    {
        // Arrange
        var before = OctSnapshot();
        var after = OctSnapshot();
        after[0].Total++;

        // Act
        var first = _service.ComputeFingerprint(123, before);
        var second = _service.ComputeFingerprint(123, after);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_GameweekPointsChange_ProducesDifferentFingerprint()
    {
        // Arrange
        var before = OctSnapshot();
        var after = OctSnapshot();
        after[0].EventTotal++;

        // Act
        var first = _service.ComputeFingerprint(123, before);
        var second = _service.ComputeFingerprint(123, after);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_EntrantAddedOrRemoved_ProducesDifferentFingerprint()
    {
        // Arrange
        var before = OctSnapshot();
        var added = OctSnapshot();
        added.Add(new ClassicStanding
        {
            Id = 4,
            Entry = 4,
            EntryName = "Newcomer",
            PlayerName = "Nina",
            Rank = 4,
            LastRank = 4,
            Total = 100,
            EventTotal = 40
        });
        var removed = OctSnapshot().Take(2).ToList();

        // Act
        var baseline = _service.ComputeFingerprint(123, before);
        var withAdded = _service.ComputeFingerprint(123, added);
        var withRemoved = _service.ComputeFingerprint(123, removed);

        // Assert
        withAdded.Should().NotBe(baseline);
        withRemoved.Should().NotBe(baseline);
    }

    [Test]
    public void ComputeFingerprint_EntryNameChange_ProducesDifferentFingerprint()
    {
        // Arrange
        var before = OctSnapshot();
        var after = OctSnapshot();
        after[0].EntryName += " II";

        // Act
        var first = _service.ComputeFingerprint(123, before);
        var second = _service.ComputeFingerprint(123, after);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_LastRankChange_ProducesDifferentFingerprint()
    {
        // Arrange: last-rank drives the movement commentary.
        var before = OctSnapshot();
        var after = OctSnapshot();
        after[0].LastRank++;

        // Act
        var first = _service.ComputeFingerprint(123, before);
        var second = _service.ComputeFingerprint(123, after);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_WinnerChange_ProducesDifferentFingerprint()
    {
        // Arrange
        var before = OctSnapshot();
        var after = OctSnapshot();
        after[1].EventTotal = 80;

        // Act
        var first = _service.ComputeFingerprint(123, before);
        var second = _service.ComputeFingerprint(123, after);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_TiedWinners_ProducesDifferentFingerprint()
    {
        // Arrange: a tie changes the winner commentary.
        var singleWinner = OctSnapshot();
        var tiedWinners = OctSnapshot();
        tiedWinners[1].EventTotal = tiedWinners[0].EventTotal;

        // Act
        var single = _service.ComputeFingerprint(123, singleWinner);
        var tied = _service.ComputeFingerprint(123, tiedWinners);

        // Assert
        tied.Should().NotBe(single);
    }

    [Test]
    public void ComputeFingerprint_NewGameweek_ProducesDifferentFingerprint()
    {
        // Arrange: totals advance and the gameweek score resets.
        var before = OctSnapshot();
        var after = OctSnapshot();
        foreach (var row in after)
        {
            row.Total += row.EventTotal;
            row.LastRank = row.Rank;
            row.EventTotal = 12;
        }

        // Act
        var first = _service.ComputeFingerprint(123, before);
        var second = _service.ComputeFingerprint(123, after);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_DifferentLeagueId_ProducesDifferentFingerprint()
    {
        // Arrange
        var snapshot = OctSnapshot();

        // Act
        var first = _service.ComputeFingerprint(123, snapshot);
        var second = _service.ComputeFingerprint(456, snapshot);

        // Assert
        second.Should().NotBe(first);
    }

    [Test]
    public void ComputeFingerprint_PlayerNameOnlyChange_ProducesSameFingerprint()
    {
        // Arrange: player names are not rendered in the scheduled post.
        var before = OctSnapshot();
        var after = OctSnapshot();
        after[0].PlayerName = "Someone Else";

        // Act
        var first = _service.ComputeFingerprint(123, before);
        var second = _service.ComputeFingerprint(123, after);

        // Assert
        second.Should().Be(first);
    }

    private static List<ClassicStanding> OctSnapshot()
    {
        return
        [
            new ClassicStanding
            {
                Id = 1,
                Entry = 11,
                EntryName = "Antony The Beaver",
                PlayerName = "Antony",
                Rank = 1,
                LastRank = 1,
                Total = 319,
                EventTotal = 74
            },
            new ClassicStanding
            {
                Id = 2,
                Entry = 22,
                EntryName = "FC Laimdots",
                PlayerName = "Laima",
                Rank = 2,
                LastRank = 2,
                Total = 295,
                EventTotal = 60
            },
            new ClassicStanding
            {
                Id = 3,
                Entry = 33,
                EntryName = "FC Jam",
                PlayerName = "Jam",
                Rank = 3,
                LastRank = 3,
                Total = 249,
                EventTotal = 55
            }
        ];
    }
}
