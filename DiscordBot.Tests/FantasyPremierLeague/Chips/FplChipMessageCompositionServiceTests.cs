using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Chips;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Chips;

[TestFixture]
public sealed class FplChipMessageCompositionServiceTests
{
    private static readonly FplChipCatalog Catalog = new();
    private static readonly FplChipMessageCompositionService Service = new();

    private static string Compose() => Service.Compose(Catalog.GetAll());

    [Test]
    public void Compose_ContainsAllFourChipNames()
    {
        var message = Compose();

        message.Should().Contain("Wildcard")
            .And.Contain("Free Hit")
            .And.Contain("Bench Boost")
            .And.Contain("Triple Captain");
    }

    [Test]
    public void Compose_ExplainsTwoChipSets()
    {
        var message = Compose();

        message.Should().Contain("две фишки")
            .And.Contain("GW19")
            .And.Contain("сгорает");
    }

    [Test]
    public void Compose_ExplainsOneChipPerGameweekRestriction()
    {
        var message = Compose();

        message.Should().Contain("только одну фишку");
    }

    [Test]
    public void Compose_FreeHit_ExplainsThatOriginalSquadReturns()
    {
        var message = Compose();

        message.Should().Contain("возвращается предыдущ");
    }

    [Test]
    public void Compose_FreeHit_ExplainsAvailabilityRestrictions()
    {
        var message = Compose();

        message.Should().Contain("после GW1")
            .And.Contain("два тура подряд");
    }

    [Test]
    public void Compose_Wildcard_ExplainsPermanentSquadChanges()
    {
        var message = Compose();

        message.Should().Contain("на постоянной основе")
            .And.Contain("отменить нельзя");
    }

    [Test]
    public void Compose_TripleCaptain_ExplainsTriplePoints()
    {
        var message = Compose();

        message.Should().Contain("утраиваются");
    }

    [Test]
    public void Compose_BenchBoost_ExplainsBenchPointsCount()
    {
        var message = Compose();

        message.Should().Contain("всех 15 игроков")
            .And.Contain("скамейк");
    }
}
