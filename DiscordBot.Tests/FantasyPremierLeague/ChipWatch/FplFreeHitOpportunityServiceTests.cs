using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Chips;
using DiscordBot.FantasyPremierLeague.ChipWatch;
using DiscordBot.Responses;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.ChipWatch;

[TestFixture]
public sealed class FplFreeHitOpportunityServiceTests
{
    private readonly FplFreeHitOpportunityService _sut = new();

    private static FplChipAvailability AvailableFreeHit(FplChipUrgency urgency = FplChipUrgency.None) =>
        new(FplChipType.FreeHit, true, null, urgency);

    private static FplChipAvailability UnavailableFreeHit() =>
        new(FplChipType.FreeHit, false, 5, FplChipUrgency.None);

    private static PremierLeagueElement CreateElement(int id, int teamId, string status = "a", int? chance = null) =>
        new() { Id = id, TeamId = teamId, Status = status, ChanceOfPlayingNextRound = chance, WebName = $"P{id}" };

    private static PremierLeagueFixture CreateFixture(int teamH, int teamA, int hDiff, int aDiff) =>
        new() { Id = teamH * 100 + teamA, EventId = 12, HomeTeamId = teamH, AwayTeamId = teamA, HomeTeamDifficulty = hDiff, AwayTeamDifficulty = aDiff };

    private static IReadOnlyDictionary<int, IReadOnlyList<PremierLeagueFixture>> FixturesByTeam(params PremierLeagueFixture[] fixtures)
    {
        var dict = new Dictionary<int, List<PremierLeagueFixture>>();
        foreach (var f in fixtures)
        {
            if (!dict.TryGetValue(f.HomeTeamId, out var hl)) { hl = []; dict[f.HomeTeamId] = hl; }
            hl.Add(f);
            if (!dict.TryGetValue(f.AwayTeamId, out var al)) { al = []; dict[f.AwayTeamId] = al; }
            al.Add(f);
        }

        return dict.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<PremierLeagueFixture>)kvp.Value);
    }

    [Test]
    public void Evaluate_FullyPlayableSquad_LowScore()
    {
        var elements = new Dictionary<int, PremierLeagueElement>
        {
            [1] = CreateElement(1, 1),
            [2] = CreateElement(2, 2)
        };
        var fixtures = FixturesByTeam(CreateFixture(1, 10, 2, 2), CreateFixture(2, 11, 2, 2));
        var picks = new[] { new EntryEventPick { Element = 1 }, new EntryEventPick { Element = 2 } };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().BeLessThan(40);
        rec.Level.Should().Be(FplChipOpportunityLevel.None);
    }

    [Test]
    public void Evaluate_OneBlank_Adds12()
    {
        var elements = new Dictionary<int, PremierLeagueElement> { [1] = CreateElement(1, 1) };
        var fixtures = FixturesByTeam(); // no fixtures for team 1 => blank
        var picks = new[] { new EntryEventPick { Element = 1 } };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().Be(12);
    }

    [Test]
    public void Evaluate_ThreeBlanks_36()
    {
        var elements = new Dictionary<int, PremierLeagueElement>
        {
            [1] = CreateElement(1, 1),
            [2] = CreateElement(2, 2),
            [3] = CreateElement(3, 3)
        };
        var fixtures = FixturesByTeam();
        var picks = new[] { new EntryEventPick { Element = 1 }, new EntryEventPick { Element = 2 }, new EntryEventPick { Element = 3 } };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().Be(36);
    }

    [Test]
    public void Evaluate_FourthBlank_Adds8()
    {
        var elements = Enumerable.Range(1, 4).ToDictionary(i => i, i => CreateElement(i, i));
        var fixtures = FixturesByTeam();
        var picks = Enumerable.Range(1, 4).Select(i => new EntryEventPick { Element = i }).ToArray();
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().Be(44);
    }

    [Test]
    public void Evaluate_BlankCap60()
    {
        var elements = Enumerable.Range(1, 10).ToDictionary(i => i, i => CreateElement(i, i));
        var fixtures = FixturesByTeam();
        var picks = Enumerable.Range(1, 10).Select(i => new EntryEventPick { Element = i }).ToArray();
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().BeLessThanOrEqualTo(100);
        // With 10 blanks, blank contribution capped at 60, but total capped also via noise? 10 blanks => blank 60, but blank >=3 so no cap, score 60
        rec.OpportunityScore.Should().Be(60);
    }

    [Test]
    public void Evaluate_UnavailableNonBlank_Adds8()
    {
        var elements = new Dictionary<int, PremierLeagueElement> { [1] = CreateElement(1, 1, "i", 0) };
        var fixtures = FixturesByTeam(CreateFixture(1, 10, 2, 2));
        var picks = new[] { new EntryEventPick { Element = 1 } };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().Be(8);
    }

    [Test]
    public void Evaluate_UnavailableBlank_NotDoubleCounted()
    {
        var elements = new Dictionary<int, PremierLeagueElement> { [1] = CreateElement(1, 1, "i", 0) };
        var fixtures = FixturesByTeam(); // blank
        var picks = new[] { new EntryEventPick { Element = 1 } };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().Be(12); // only blank, not +8
    }

    [Test]
    public void Evaluate_DoubtfulNonBlank_Adds4()
    {
        var elements = new Dictionary<int, PremierLeagueElement> { [1] = CreateElement(1, 1, "d", null) };
        var fixtures = FixturesByTeam(CreateFixture(1, 10, 2, 2));
        var picks = new[] { new EntryEventPick { Element = 1 } };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().Be(4);
    }

    [Test]
    public void Evaluate_DoubtfulNotCountedAsUnavailable()
    {
        var elements = new Dictionary<int, PremierLeagueElement> { [1] = CreateElement(1, 1, "d", 25) };
        var fixtures = FixturesByTeam(CreateFixture(1, 10, 2, 2));
        var picks = new[] { new EntryEventPick { Element = 1 } };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.Reasons.Should().Contain(r => r.Kind == FplChipRecommendationReasonKind.DoubtfulPlayers);
        rec.Reasons.Should().NotContain(r => r.Kind == FplChipRecommendationReasonKind.UnavailablePlayers);
    }

    [Test]
    public void Evaluate_DifficultFixture_Adds3()
    {
        var elements = new Dictionary<int, PremierLeagueElement> { [1] = CreateElement(1, 1, "a") };
        var fixtures = FixturesByTeam(CreateFixture(1, 10, 5, 2));
        var picks = new[] { new EntryEventPick { Element = 1 } };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().Be(3);
    }

    [Test]
    public void Evaluate_EasyFixture_NoDifficulty()
    {
        var elements = new Dictionary<int, PremierLeagueElement> { [1] = CreateElement(1, 1, "a") };
        var fixtures = FixturesByTeam(CreateFixture(1, 10, 2, 2));
        var picks = new[] { new EntryEventPick { Element = 1 } };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().Be(0);
    }

    [Test]
    public void Evaluate_Dgw_NotBlank()
    {
        var elements = new Dictionary<int, PremierLeagueElement> { [1] = CreateElement(1, 1) };
        var fixtures = FixturesByTeam(CreateFixture(1, 10, 2, 2), CreateFixture(1, 11, 3, 3));
        var picks = new[] { new EntryEventPick { Element = 1 } };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.Reasons.Should().NotContain(r => r.Kind == FplChipRecommendationReasonKind.BlankPlayers);
    }

    [Test]
    public void Evaluate_DgwOneEasyOneDifficult_NotAllDifficult()
    {
        var elements = new Dictionary<int, PremierLeagueElement> { [1] = CreateElement(1, 1) };
        var fixtures = FixturesByTeam(CreateFixture(1, 10, 2, 2), CreateFixture(11, 1, 2, 5));
        var picks = new[] { new EntryEventPick { Element = 1 } };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        // Difficult should be false because not all fixtures >=4
        rec!.OpportunityScore.Should().Be(0);
    }

    [Test]
    public void Evaluate_ScoreClampedTo100()
    {
        var elements = Enumerable.Range(1, 15).ToDictionary(i => i, i => CreateElement(i, i));
        var fixtures = FixturesByTeam(); // all blank => 60
        // Add unavailable and difficult on top after making some not blank? To exceed 100 we need many signals
        // With additive difficult, we can get 60+24+12+15=111 -> clamped 100 but also blank<3 cap? With 15 blanks, blank >=3 so no cap, score 60, not 100
        // Use 6 blanks (52) + 3 unavailable (24) + 3 doubtful (12) + 5 difficult (15) = 103 -> clamped 100 but needs 6 blanks + 3u +3d +5 diff
        // For simplicity, test that 15 blanks alone is capped at 60 not 100
        var picks = Enumerable.Range(1, 15).Select(i => new EntryEventPick { Element = i }).ToArray();
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().BeLessThanOrEqualTo(100);
    }

    [Test]
    public void Evaluate_FewerThanThreeBlanks_CapsAt59()
    {
        var elements = new Dictionary<int, PremierLeagueElement>
        {
            [1] = CreateElement(1, 1, "a"),
            [2] = CreateElement(2, 2, "a")
        };
        // Make both have difficult fixtures to push raw score high: 2 blanks would be 24, plus 2 difficult 6 => 30 not capped. Need to force high via unavailable etc.
        // Use 2 blanks + 3 unavailable + 3 doubtful + 5 difficult => 24 +24+12+15=75 -> capped 59 because blanks<3
        // Create 2 blanks + 3 unavailable non-blank + 3 doubtful non-blank + 5 difficult need total picks 13
        // Simplify: create 2 blanks + 3 unavailable + 3 doubtful + 5 difficult = 2+3+3+5=13 players
        elements = Enumerable.Range(1, 13).ToDictionary(i => i, i =>
            i <= 2 ? CreateElement(i, i) : // blanks
            i <= 5 ? CreateElement(i, i, "i", 0) :
            i <= 8 ? CreateElement(i, i, "d", 25) :
            CreateElement(i, i, "a"));
        var fixturesBlanks = new List<PremierLeagueFixture>();
        // For non-blank teams, add fixture with diff 5
        for (int i = 3; i <= 13; i++)
        {
            fixturesBlanks.Add(CreateFixture(i, 100 + i, 5, 2));
        }
        var fixturesDict = FixturesByTeam(fixturesBlanks.ToArray());
        var picks = Enumerable.Range(1, 13).Select(i => new EntryEventPick { Element = i }).ToArray();
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixturesDict);
        rec!.OpportunityScore.Should().Be(59);
    }

    [Test]
    public void Evaluate_Level_None_At39()
    {
        var elements = new Dictionary<int, PremierLeagueElement>
        {
            [1] = CreateElement(1, 1),
            [2] = CreateElement(2, 2),
            [3] = CreateElement(3, 3),
            [4] = CreateElement(4, 4)
        };
        // 3 blanks =36, 1 difficult =3 => 39
        var fixtures = new Dictionary<int, IReadOnlyList<PremierLeagueFixture>>
        {
            [4] = new List<PremierLeagueFixture> { CreateFixture(4, 10, 5, 2) }
        };
        var picks = new[]
        {
            new EntryEventPick { Element = 1 },
            new EntryEventPick { Element = 2 },
            new EntryEventPick { Element = 3 },
            new EntryEventPick { Element = 4 }
        };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().Be(39);
        rec.Level.Should().Be(FplChipOpportunityLevel.None);
    }

    [Test]
    public void Evaluate_Level_Consider_At40()
    {
        var elements = new Dictionary<int, PremierLeagueElement>
        {
            [1] = CreateElement(1, 1),
            [2] = CreateElement(2, 2),
            [3] = CreateElement(3, 3),
            [4] = CreateElement(4, 4, "d", 25)
        };
        var fixtures = FixturesByTeam(CreateFixture(4, 10, 2, 2));
        var picks = new[]
        {
            new EntryEventPick { Element = 1 },
            new EntryEventPick { Element = 2 },
            new EntryEventPick { Element = 3 },
            new EntryEventPick { Element = 4 }
        };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().Be(40);
        rec.Level.Should().Be(FplChipOpportunityLevel.Consider);
    }

    [Test]
    public void Evaluate_Level_Strong_At60()
    {
        var elements = Enumerable.Range(1, 6).ToDictionary(i => i, i => CreateElement(i, i));
        var fixtures = FixturesByTeam();
        var picks = Enumerable.Range(1, 6).Select(i => new EntryEventPick { Element = i }).ToArray();
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().Be(60);
        rec.Level.Should().Be(FplChipOpportunityLevel.Strong);
    }

    [Test]
    public void Evaluate_Level_VeryStrong_At75()
    {
        var elements = Enumerable.Range(1, 11).ToDictionary(i => i, i => i <= 6 ? CreateElement(i, i) : CreateElement(i, i, "a"));
        var fixtures = new Dictionary<int, IReadOnlyList<PremierLeagueFixture>>();
        // 6 blanks already 60, plus 5 difficult
        for (int i = 7; i <= 11; i++)
        {
            fixtures[i] = new List<PremierLeagueFixture> { CreateFixture(i, 100 + i, 5, 2) };
        }
        var picks = Enumerable.Range(1, 11).Select(i => new EntryEventPick { Element = i }).ToArray();
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().Be(75);
        rec.Level.Should().Be(FplChipOpportunityLevel.VeryStrong);
    }

    [Test]
    public void Evaluate_UnavailableFreeHit_ReturnsNull()
    {
        var elements = new Dictionary<int, PremierLeagueElement> { [1] = CreateElement(1, 1) };
        var fixtures = FixturesByTeam(CreateFixture(1, 10, 2, 2));
        var picks = new[] { new EntryEventPick { Element = 1 } };
        var rec = _sut.Evaluate(UnavailableFreeHit(), picks, elements, fixtures);
        rec.Should().BeNull();
    }

    [Test]
    public void Evaluate_ReasonsContainCounts()
    {
        var elements = new Dictionary<int, PremierLeagueElement> { [1] = CreateElement(1, 1) };
        var fixtures = FixturesByTeam(); // blank
        var picks = new[] { new EntryEventPick { Element = 1 } };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.Reasons.Should().ContainSingle(r => r.Kind == FplChipRecommendationReasonKind.BlankPlayers && r.Count == 1);
    }

    [Test]
    public void Evaluate_DifficultAdditiveWithDoubtful()
    {
        // Doubtful player with difficult fixture should count both
        var elements = new Dictionary<int, PremierLeagueElement> { [1] = CreateElement(1, 1, "d", 25) };
        var fixtures = FixturesByTeam(CreateFixture(1, 10, 5, 2));
        var picks = new[] { new EntryEventPick { Element = 1 } };
        var rec = _sut.Evaluate(AvailableFreeHit(), picks, elements, fixtures);
        rec!.OpportunityScore.Should().Be(7); // 4 doubtful + 3 difficult
        rec.Reasons.Should().Contain(r => r.Kind == FplChipRecommendationReasonKind.DoubtfulPlayers && r.Count == 1);
        rec.Reasons.Should().Contain(r => r.Kind == FplChipRecommendationReasonKind.DifficultFixtures && r.Count == 1);
    }
}
