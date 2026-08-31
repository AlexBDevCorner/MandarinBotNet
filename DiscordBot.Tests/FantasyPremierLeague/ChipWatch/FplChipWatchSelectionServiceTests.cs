using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.ChipWatch;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.ChipWatch;

[TestFixture]
public sealed class FplChipWatchSelectionServiceTests
{
    private readonly FplChipWatchSelectionService _sut = new();

    private static FplManagerChipWatch CreateManager(int id, string team, string player) =>
        new(id, team, player, 10, [], null, true, true);

    private static readonly FplManagerChipWatch[] Managers =
    [
        CreateManager(1, "FC Maguire", "John"),
        CreateManager(2, "VARchester", "Mike"),
        CreateManager(3, "Bottlejobs United", "Alex")
    ];

    [Test]
    public void Select_ExactTeamName_Found()
    {
        var result = _sut.Select(Managers, "FC Maguire");
        result.Status.Should().Be(FplChipWatchSelectionStatus.Found);
        result.Manager!.EntryName.Should().Be("FC Maguire");
    }

    [Test]
    public void Select_ExactTeamCaseInsensitive_Found()
    {
        var result = _sut.Select(Managers, "fc maguire");
        result.Status.Should().Be(FplChipWatchSelectionStatus.Found);
    }

    [Test]
    public void Select_ExactManagerName_Found()
    {
        var result = _sut.Select(Managers, "Mike");
        result.Status.Should().Be(FplChipWatchSelectionStatus.Found);
        result.Manager!.PlayerName.Should().Be("Mike");
    }

    [Test]
    public void Select_UniquePartialTeam_Found()
    {
        var result = _sut.Select(Managers, "Maguire");
        result.Status.Should().Be(FplChipWatchSelectionStatus.Found);
    }

    [Test]
    public void Select_UniquePartialManager_Found()
    {
        var result = _sut.Select(Managers, "Alex");
        result.Status.Should().Be(FplChipWatchSelectionStatus.Found);
    }

    [Test]
    public void Select_AmbiguousPartial_ReturnsAmbiguous()
    {
        var managers = new[]
        {
            CreateManager(1, "FC Maguire", "John"),
            CreateManager(2, "FC Maguire Reserves", "John2")
        };
        var result = _sut.Select(managers, "Maguire");
        result.Status.Should().Be(FplChipWatchSelectionStatus.Ambiguous);
        result.Candidates.Should().HaveCount(2);
    }

    [Test]
    public void Select_NotFound_ReturnsNotFound()
    {
        var result = _sut.Select(Managers, "Nonexistent");
        result.Status.Should().Be(FplChipWatchSelectionStatus.NotFound);
    }

    [Test]
    public void Select_ExactWinsOverPartial()
    {
        var managers = new[]
        {
            CreateManager(1, "FC Maguire", "John"),
            CreateManager(2, "FC Maguire United", "Mike")
        };
        var result = _sut.Select(managers, "FC Maguire");
        result.Status.Should().Be(FplChipWatchSelectionStatus.Found);
        result.Manager!.EntryId.Should().Be(1);
    }
}
