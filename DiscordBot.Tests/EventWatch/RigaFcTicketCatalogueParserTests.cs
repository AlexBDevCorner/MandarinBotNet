using AwesomeAssertions;
using DiscordBot.EventWatch;
using NUnit.Framework;

namespace DiscordBot.Tests.EventWatch;

[TestFixture]
public sealed class RigaFcTicketCatalogueParserTests
{
    private RigaFcTicketCatalogueParser _parser = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new RigaFcTicketCatalogueParser();
    }

    [Test]
    public void Parse_EmptyItems_ReturnsNoObservations()
    {
        var observations = _parser.Parse(
            """{"status":"success","total":0,"items":[]}""",
            RigaFcClient.TicketCatalogueApiUri);

        observations.Should().BeEmpty();
    }

    [Test]
    public void Parse_GenericConferenceLeaguePackage_ReturnsProductObservations()
    {
        var observations = _parser.Parse(
            CatalogueFixtures.GenericPackage(),
            RigaFcClient.TicketCatalogueApiUri);

        observations.Should().HaveCount(2);
        observations.Should().OnlyContain(o => o.Anchors.Count == 1);
        observations[0].Text.Should().Contain("UEFA Conference League");
        observations[0].SourceUrl.Should().Be(
            "https://www.bilesuserviss.lv/biletes/CDYQ7TOEZE/uefa-conference-league-bilete-uz-tris-majas-spelem");
        observations[0].Context.Should().Contain("Skonto stadions");
    }

    [Test]
    public void Parse_KairatProduct_ExposesProductUrl()
    {
        var observations = _parser.Parse(
            CatalogueFixtures.WithKairat(),
            RigaFcClient.TicketCatalogueApiUri);

        var kairat = observations.Should().ContainSingle(o => o.Text.Contains("Kairat")).Subject;
        kairat.SourceUrl.Should().Be("https://www.bilesuserviss.lv/biletes/KAIRAT01/riga-fc-vs-kairat-almaty");
        kairat.Anchors.Should().ContainSingle().Which.Url.Should().Be(kairat.SourceUrl);
    }

    [Test]
    public void Parse_InvalidJson_ThrowsObservably()
    {
        Func<IReadOnlyList<EventWatchObservation>> act = () => _parser.Parse(
            "not-json",
            RigaFcClient.TicketCatalogueApiUri);

        act.Should().Throw<RigaFcApiException>();
    }

    [Test]
    public void Parse_MissingItems_ThrowsObservably()
    {
        Func<IReadOnlyList<EventWatchObservation>> act = () => _parser.Parse(
            """{"status":"success"}""",
            RigaFcClient.TicketCatalogueApiUri);

        act.Should().Throw<RigaFcApiException>();
    }

    [Test]
    public void Parse_ItemsWithoutNameOrId_AreSkipped()
    {
        var json = """{"status":"success","items":[{"id":"","name":""},{"id":"X1"},{"name":"Nameless"}]}""";

        var observations = _parser.Parse(json, RigaFcClient.TicketCatalogueApiUri);

        observations.Should().BeEmpty();
    }

    [Test]
    public void Parse_DuplicateProducts_AreCollapsed()
    {
        var json = """{"status":"success","items":[{"id":"DUP01","status":"ON_SALE","name":"Riga FC vs Kairat Almaty","sluggedName":"riga-fc-vs-kairat","venue":{"name":"Skonto stadions"}},{"id":"DUP01","status":"ON_SALE","name":"Riga FC vs Kairat Almaty","sluggedName":"riga-fc-vs-kairat","venue":{"name":"Skonto stadions"}}]}""";

        var observations = _parser.Parse(json, RigaFcClient.TicketCatalogueApiUri);

        // Two catalogue entries resolving to the same product URL collapse
        // into one scoped observation.
        observations.Should().ContainSingle();
        observations[0].Text.Should().Contain("Kairat");
    }

    [Test]
    public void BuildProductUri_WithoutSlug_FallsBackToIdOnly()
    {
        RigaFcTicketCatalogueParser.BuildProductUri("ABC123", null)
            .Should().Be("https://www.bilesuserviss.lv/biletes/ABC123");
        RigaFcTicketCatalogueParser.BuildProductUri("ABC123", "  ")
            .Should().Be("https://www.bilesuserviss.lv/biletes/ABC123");
    }
}
