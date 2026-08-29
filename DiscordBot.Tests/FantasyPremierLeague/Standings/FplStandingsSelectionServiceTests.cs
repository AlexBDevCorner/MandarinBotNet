using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Standings;
using DiscordBot.Responses;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Standings;

[TestFixture]
public sealed class FplStandingsSelectionServiceTests
{
    private static readonly FplStandingsSelectionService Service = new();

    [Test]
    public void SelectClassic_UsesEventTotalAndTotal()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 1,
                Entry = 1,
                EntryName = "Team A",
                PlayerName = "Manager A",
                EventTotal = 63,
                Total = 1247,
                LastRank = 0
            }
        };

        var view = Service.SelectClassic(standings, top: null);

        var row = view.Rows.Should().ContainSingle().Which;
        row.GameweekPoints.Should().Be(63);
        row.TotalPoints.Should().Be(1247);
        row.Rank.Should().Be(1);
        row.EntryName.Should().Be("Team A");
        row.ManagerName.Should().Be("Manager A");
        row.Movement.Should().Be(FplStandingsMovement.None);
        row.HasGap.Should().BeFalse();
    }

    [Test]
    public void SelectClassic_CalculatesRankUpMovement()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 5,
                Entry = 1,
                EntryName = "Team",
                EventTotal = 10,
                Total = 1000,
                LastRank = 8
            }
        };

        var row = Service.SelectClassic(standings, top: null).Rows.Single();

        row.Movement.Should().Be(FplStandingsMovement.Up);
        row.MovementDelta.Should().Be(3);
    }

    [Test]
    public void SelectClassic_CalculatesRankDownMovement()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 4,
                Entry = 1,
                EntryName = "Team",
                EventTotal = 10,
                Total = 1000,
                LastRank = 3
            }
        };

        var row = Service.SelectClassic(standings, top: null).Rows.Single();

        row.Movement.Should().Be(FplStandingsMovement.Down);
        row.MovementDelta.Should().Be(1);
    }

    [Test]
    public void SelectClassic_UnchangedRankHasNoMovement()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 3,
                Entry = 1,
                EntryName = "Team",
                EventTotal = 10,
                Total = 1000,
                LastRank = 3
            }
        };

        var row = Service.SelectClassic(standings, top: null).Rows.Single();

        row.Movement.Should().Be(FplStandingsMovement.None);
        row.MovementDelta.Should().Be(0);
    }

    [Test]
    public void SelectClassic_InvalidLastRankHasNoMovement()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 2,
                Entry = 1,
                EntryName = "Team",
                EventTotal = 10,
                Total = 1000,
                LastRank = 0
            }
        };

        var row = Service.SelectClassic(standings, top: null).Rows.Single();

        row.Movement.Should().Be(FplStandingsMovement.None);
        row.MovementDelta.Should().Be(0);
    }

    [Test]
    public void SelectClassic_GapCalculatedRelativeToRealLeader()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 1,
                Entry = 1,
                EntryName = "Leader",
                EventTotal = 10,
                Total = 1247,
                LastRank = 0
            },
            new ClassicStanding
            {
                Rank = 2,
                Entry = 2,
                EntryName = "Chaser",
                EventTotal = 10,
                Total = 1219,
                LastRank = 0
            }
        };

        var rows = Service.SelectClassic(standings, top: null).Rows;

        rows[0].HasGap.Should().BeFalse();
        rows[1].HasGap.Should().BeTrue();
        rows[1].GapToLeader.Should().Be(28);
    }

    [Test]
    public void SelectClassic_TopLimitsToFirstRows()
    {
        var standings = Enumerable.Range(1, 10)
            .Select(rank => new ClassicStanding
            {
                Rank = rank,
                Entry = rank,
                EntryName = $"Team {rank}",
                EventTotal = 10,
                Total = 110 - rank
            })
            .ToArray();

        var rows = Service.SelectClassic(standings, top: 5).Rows;

        rows.Should().HaveCount(5);
        rows[0].Rank.Should().Be(1);
        rows[4].Rank.Should().Be(5);
    }

    [Test]
    public void SelectClassic_TopLargerThanLeagueReturnsAll()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 1,
                Entry = 1,
                EntryName = "Team 1",
                Total = 100
            },
            new ClassicStanding
            {
                Rank = 2,
                Entry = 2,
                EntryName = "Team 2",
                Total = 99
            }
        };

        var rows = Service.SelectClassic(standings, top: 20).Rows;

        rows.Should().HaveCount(2);
    }

    [Test]
    public void SelectClassic_TopDoesNotChangeGapCalculation()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 1,
                Entry = 1,
                EntryName = "Leader",
                Total = 200
            },
            new ClassicStanding
            {
                Rank = 9,
                Entry = 9,
                EntryName = "Ninth",
                Total = 150
            }
        };

        var rows = Service.SelectClassic(standings, top: 1).Rows;

        rows.Should().ContainSingle();
        rows[0].Rank.Should().Be(1);
        rows[0].GapToLeader.Should().Be(0);
    }

    [Test]
    public void ResolveAround_ExactTeamNameMatch()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 1,
                Entry = 1,
                EntryName = "Bobrov FC",
                PlayerName = "Alex",
                Total = 100
            }
        };

        var result = Service.ResolveAround(standings, "Bobrov FC");

        result.Status.Should().Be(FplAroundLookupStatus.Available);
        result.Rows.Should().ContainSingle().Which.EntryName.Should().Be("Bobrov FC");
    }

    [Test]
    public void ResolveAround_ExactManagerNameMatch()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 1,
                Entry = 1,
                EntryName = "Bobrov FC",
                PlayerName = "Aleksandrs",
                Total = 100
            }
        };

        var result = Service.ResolveAround(standings, "Aleksandrs");

        result.Status.Should().Be(FplAroundLookupStatus.Available);
        result.Rows.Should().ContainSingle().Which.ManagerName.Should().Be("Aleksandrs");
    }

    [Test]
    public void ResolveAround_CaseInsensitiveMatch()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 1,
                Entry = 1,
                EntryName = "Bobrov FC",
                Total = 100
            }
        };

        var result = Service.ResolveAround(standings, "bobrov fc");

        result.Status.Should().Be(FplAroundLookupStatus.Available);
    }

    [Test]
    public void ResolveAround_PartialMatch()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 1,
                Entry = 1,
                EntryName = "Bobrov FC",
                Total = 100
            }
        };

        var result = Service.ResolveAround(standings, "Bobrov");

        result.Status.Should().Be(FplAroundLookupStatus.Available);
        result.Rows.Should().ContainSingle().Which.EntryName.Should().Be("Bobrov FC");
    }

    [Test]
    public void ResolveAround_ExactWinsOverPartial()
    {
        var standings = Enumerable.Range(1, 10)
            .Select(rank => new ClassicStanding
            {
                Rank = rank,
                Entry = rank,
                EntryName = rank == 1 ? "Alex" : $"Alex Friend {rank}",
                PlayerName = $"Manager {rank}",
                Total = 110 - rank
            })
            .ToArray();

        var result = Service.ResolveAround(standings, "Alex");

        result.Status.Should().Be(FplAroundLookupStatus.Available);
        result.Rows.Should().ContainSingle(row => row.IsAroundTarget)
            .Which.EntryName.Should().Be("Alex");
    }

    [Test]
    public void ResolveAround_NoMatch_ReturnsNotFound()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 1,
                Entry = 1,
                EntryName = "Bobrov FC",
                Total = 100
            }
        };

        var result = Service.ResolveAround(standings, "Ghost");

        result.Status.Should().Be(FplAroundLookupStatus.NotFound);
    }

    [Test]
    public void ResolveAround_AmbiguousPartialMatch_ReturnsCandidates()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 1,
                Entry = 1,
                EntryName = "Alex FC",
                PlayerName = "Bob",
                Total = 100
            },
            new ClassicStanding
            {
                Rank = 2,
                Entry = 2,
                EntryName = "Alex Smith",
                PlayerName = "Smith",
                Total = 99
            }
        };

        var result = Service.ResolveAround(standings, "Alex");

        result.Status.Should().Be(FplAroundLookupStatus.Ambiguous);
        result.Candidates.Should().HaveCount(2);
    }

    [Test]
    public void ResolveAround_MiddleOfTable_WindowHasTwoEachSide()
    {
        var standings = Enumerable.Range(1, 10)
            .Select(rank => new ClassicStanding
            {
                Rank = rank,
                Entry = rank,
                EntryName = $"Team {rank}",
                Total = 110 - rank
            })
            .ToArray();

        var result = Service.ResolveAround(standings, "Team 8");

        result.Status.Should().Be(FplAroundLookupStatus.Available);
        result.Rows.Should().HaveCount(5);
        result.Rows[0].EntryName.Should().Be("Team 6");
        result.Rows[4].EntryName.Should().Be("Team 10");
        result.Rows.Should().ContainSingle(row => row.IsAroundTarget && row.EntryName == "Team 8");
    }

    [Test]
    public void ResolveAround_TopOfTable_ShiftsWindowDown()
    {
        var standings = Enumerable.Range(1, 10)
            .Select(rank => new ClassicStanding
            {
                Rank = rank,
                Entry = rank,
                EntryName = $"Team {rank}",
                Total = 110 - rank
            })
            .ToArray();

        var result = Service.ResolveAround(standings, "Team 1");

        result.Rows.Should().HaveCount(5);
        result.Rows[0].EntryName.Should().Be("Team 1");
        result.Rows[4].EntryName.Should().Be("Team 5");
    }

    [Test]
    public void ResolveAround_BottomOfTable_ShiftsWindowUp()
    {
        var standings = Enumerable.Range(1, 10)
            .Select(rank => new ClassicStanding
            {
                Rank = rank,
                Entry = rank,
                EntryName = $"Team {rank}",
                Total = 110 - rank
            })
            .ToArray();

        var result = Service.ResolveAround(standings, "Team 10");

        result.Rows.Should().HaveCount(5);
        result.Rows[0].EntryName.Should().Be("Team 6");
        result.Rows[4].EntryName.Should().Be("Team 10");
    }

    [Test]
    public void ResolveAround_FewerThanFiveMembers_ShowsAll()
    {
        var standings = Enumerable.Range(1, 3)
            .Select(rank => new ClassicStanding
            {
                Rank = rank,
                Entry = rank,
                EntryName = $"Team {rank}",
                Total = 110 - rank
            })
            .ToArray();

        var result = Service.ResolveAround(standings, "Team 2");

        result.Rows.Should().HaveCount(3);
        result.Rows.Should().ContainSingle(row => row.IsAroundTarget && row.EntryName == "Team 2");
    }

    [Test]
    public void SelectClassic_DeterministicOrderWithTiedRanks()
    {
        var standings = new[]
        {
            new ClassicStanding
            {
                Rank = 1,
                Entry = 2,
                EntryName = "B",
                Total = 100
            },
            new ClassicStanding
            {
                Rank = 1,
                Entry = 1,
                EntryName = "A",
                Total = 100
            },
            new ClassicStanding
            {
                Rank = 3,
                Entry = 3,
                EntryName = "C",
                Total = 90
            }
        };

        var rows = Service.SelectClassic(standings, top: null).Rows;

        rows.Should().HaveCount(3);
        rows[0].EntryName.Should().Be("A");
        rows[1].EntryName.Should().Be("B");
        rows[2].EntryName.Should().Be("C");
        rows[0].HasGap.Should().BeFalse();
        rows[1].HasGap.Should().BeFalse();
        rows[2].GapToLeader.Should().Be(10);
    }

    [Test]
    public void SelectHeadToHead_CalculatesMovementGapAndMatches()
    {
        var standings = new[]
        {
            new HeadToHeadStanding
            {
                Rank = 1,
                EntryName = "Leader",
                Total = 18,
                LastRank = 0,
                MatchesPlayed = 7
            },
            new HeadToHeadStanding
            {
                Rank = 2,
                EntryName = "Chaser",
                Total = 15,
                LastRank = 3,
                MatchesPlayed = 7
            }
        };

        var rows = Service.SelectHeadToHead(standings, top: null).Rows;

        rows[0].MatchesPlayed.Should().Be(7);
        rows[0].HasGap.Should().BeFalse();
        rows[1].Movement.Should().Be(FplStandingsMovement.Up);
        rows[1].MovementDelta.Should().Be(1);
        rows[1].GapToLeader.Should().Be(3);
    }

    [Test]
    public void SelectHeadToHead_TopLimitsRows()
    {
        var standings = Enumerable.Range(1, 10)
            .Select(rank => new HeadToHeadStanding
            {
                Rank = rank,
                EntryName = $"Team {rank}",
                Total = 110 - rank,
                MatchesPlayed = 7
            })
            .ToArray();

        var rows = Service.SelectHeadToHead(standings, top: 3).Rows;

        rows.Should().HaveCount(3);
        rows[0].Rank.Should().Be(1);
        rows[2].Rank.Should().Be(3);
    }
}
