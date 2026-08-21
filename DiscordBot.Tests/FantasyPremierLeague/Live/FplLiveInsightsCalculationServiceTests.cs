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
    public void Calculate_LivePointsAndAlerts_UsesConfiguredLeagueAndIncludesTies()
    {
        // Arrange
        var fixture = CreateFixture();
        var service = new FplLiveInsightsCalculationService(
            new FantasyPremierLeagueOptions
            {
                LargeBenchPointsThreshold = 8,
                CaptainSuccessEffectivePointsThreshold = 20,
                CaptainDisasterPointsThreshold = 2,
                CaptainDisasterViceCaptainPointsThreshold = 8
            });

        // Act
        var result = service.Calculate(
            "2026/27",
            5,
            SourceUpdatedAt,
            CapturedAt,
            fixture.Standings,
            fixture.PicksByEntry,
            fixture.Players,
            fixture.LivePlayers);

        // Assert
        result.Managers.Select(manager => manager.EntryName)
            .Should().Equal("Alpha", "Beta", "Gamma");
        result.Managers[0].LivePoints.Should().Be(12);
        result.Managers[0].PlayersRemainingToPlay.Should().Be(2);
        result.Managers[0].BenchPoints.Should().Be(17);
        result.BenchAlerts.Select(manager => manager.EntryName)
            .Should().Equal("Alpha", "Beta", "Gamma");
        result.CaptainDisasters.Select(manager => manager.EntryName)
            .Should().Equal("Alpha");
        result.CaptainSuccesses.Select(manager => manager.EntryName)
            .Should().Equal("Beta", "Gamma");
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
    public void Calculate_MissingPlayerLiveData_RejectsIncompleteSource()
    {
        // Arrange
        var fixture = CreateFixture();
        fixture.LivePlayers.Remove(1);
        var service = new FplLiveInsightsCalculationService(
            new FantasyPremierLeagueOptions());

        // Act
        var act = () => service.Calculate(
            "2026/27",
            5,
            SourceUpdatedAt,
            CapturedAt,
            fixture.Standings,
            fixture.PicksByEntry,
            fixture.Players,
            fixture.LivePlayers);

        // Assert
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*live response*player 1*");
    }

    private static Fixture CreateFixture()
    {
        return new Fixture
        {
            Standings =
            [
                new ClassicStanding
                {
                    Entry = 101,
                    EntryName = "Alpha",
                    PlayerName = "Alice",
                    Rank = 1
                },
                new ClassicStanding
                {
                    Entry = 202,
                    EntryName = "Beta",
                    PlayerName = "Bob",
                    Rank = 2
                },
                new ClassicStanding
                {
                    Entry = 303,
                    EntryName = "Gamma",
                    PlayerName = "Cara",
                    Rank = 3
                }
            ],
            PicksByEntry = new Dictionary<int, EntryEventPicksResponse>
            {
                [101] = new EntryEventPicksResponse
                {
                    Picks =
                    [
                        Pick(1, 1, 2, isCaptain: true),
                        Pick(2, 2, 1, isViceCaptain: true),
                        Pick(5, 3, 1),
                        Pick(3, 12, 0),
                        Pick(4, 13, 0)
                    ],
                    AutomaticSubstitutions =
                    [
                        new EntryAutomaticSubstitution
                        {
                            ElementIn = 4,
                            ElementOut = 5
                        }
                    ]
                },
                [202] = new EntryEventPicksResponse
                {
                    Picks =
                    [
                        Pick(6, 1, 2, isCaptain: true),
                        Pick(7, 2, 1, isViceCaptain: true),
                        Pick(9, 3, 1),
                        Pick(8, 12, 0)
                    ]
                },
                [303] = new EntryEventPicksResponse
                {
                    Picks =
                    [
                        Pick(10, 1, 2, isCaptain: true),
                        Pick(11, 2, 1, isViceCaptain: true),
                        Pick(12, 3, 1),
                        Pick(13, 12, 0)
                    ]
                }
            },
            Players = Enumerable.Range(1, 13)
                .ToDictionary(id => id, id => new PremierLeagueElement
                {
                    Id = id,
                    WebName = id switch
                    {
                        4 => "Bench In",
                        5 => "Starter Out",
                        _ => $"Player {id}"
                    }
                }),
            LivePlayers = new Dictionary<int, EventLiveElementStats>
            {
                [1] = Stats(1, 90),
                [2] = Stats(10, 0),
                [3] = Stats(8, 90),
                [4] = Stats(9, 90),
                [5] = Stats(0, 0),
                [6] = Stats(10, 90),
                [7] = Stats(1, 90),
                [8] = Stats(8, 90),
                [9] = Stats(5, 0),
                [10] = Stats(10, 90),
                [11] = Stats(1, 90),
                [12] = Stats(5, 90),
                [13] = Stats(8, 90)
            }
        };
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

    private static EventLiveElementStats Stats(int totalPoints, int minutes)
    {
        return new EventLiveElementStats
        {
            TotalPoints = totalPoints,
            Minutes = minutes
        };
    }

    private sealed class Fixture
    {
        public required IReadOnlyList<ClassicStanding> Standings { get; init; }

        public required Dictionary<int, EntryEventPicksResponse> PicksByEntry { get; init; }

        public required Dictionary<int, PremierLeagueElement> Players { get; init; }

        public required Dictionary<int, EventLiveElementStats> LivePlayers { get; init; }
    }
}
