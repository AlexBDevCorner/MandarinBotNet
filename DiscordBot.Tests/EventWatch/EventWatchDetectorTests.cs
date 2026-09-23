using AwesomeAssertions;
using DiscordBot;
using DiscordBot.EventWatch;
using NUnit.Framework;

namespace DiscordBot.Tests.EventWatch;

[TestFixture]
public sealed class EventWatchDetectorTests
{
    private RigaFcTicketCatalogueParser _parser = null!;
    private EventWatchSignalDetector _detector = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new RigaFcTicketCatalogueParser();
        _detector = new EventWatchSignalDetector();
    }

    private static DiscordBot.EventWatchDefinition CreateKairatWatch()
    {
        return new DiscordBot.EventWatchDefinition
        {
            Enabled = true,
            Id = "riga-fc-smoke-kairat-2026",
            Title = "Riga FC vs Kairat tickets",
            MatchTerms = ["Kairat"],
            Targets =
            [
                new NotificationTargetOptions { GuildId = 1, ChannelId = 2 }
            ]
        };
    }

    private static DiscordBot.EventWatchDefinition CreateAtalantaWatch()
    {
        return new DiscordBot.EventWatchDefinition
        {
            Enabled = true,
            Id = "riga-fc-atalanta-2026",
            Title = "Riga FC vs Atalanta tickets",
            MatchTerms = ["Atalanta"],
            Targets =
            [
                new NotificationTargetOptions { GuildId = 1, ChannelId = 2 }
            ]
        };
    }

    private IReadOnlyList<EventWatchObservation> ParseCatalogue(string json)
    {
        return _parser.Parse(json, RigaFcClient.TicketCatalogueApiUri);
    }

    [Test]
    public void Detect_EmptyCatalogue_NoSignal()
    {
        var observations = ParseCatalogue(CatalogueFixtures.Empty());

        _detector.Detect(CreateKairatWatch(), observations).Should().BeEmpty();
        _detector.Detect(CreateAtalantaWatch(), observations).Should().BeEmpty();
    }

    [Test]
    public void Detect_GenericConferenceLeaguePackage_NoOpponentSignal()
    {
        var observations = ParseCatalogue(CatalogueFixtures.GenericPackage());

        observations.Should().NotBeEmpty();
        _detector.Detect(CreateKairatWatch(), observations).Should().BeEmpty();
        _detector.Detect(CreateAtalantaWatch(), observations).Should().BeEmpty();
    }

    [Test]
    public void Detect_GenericTicketsWordingWithoutOpponent_NoSignal()
    {
        var observations = new List<EventWatchObservation>
        {
            new(
                "https://www.bilesuserviss.lv/biletes/GENERIC/generic-tickets",
                "Buy tickets now",
                [new EventWatchAnchor("Tickets", "https://www.bilesuserviss.lv/biletes/GENERIC/generic-tickets")],
                "Skonto stadions | ON_SALE")
        };

        _detector.Detect(CreateKairatWatch(), observations).Should().BeEmpty();
        _detector.Detect(CreateAtalantaWatch(), observations).Should().BeEmpty();
    }

    [Test]
    public void Detect_KairatProduct_ReturnsSingleKairatSignal()
    {
        var observations = ParseCatalogue(CatalogueFixtures.WithKairat());

        var signals = _detector.Detect(CreateKairatWatch(), observations);

        signals.Should().ContainSingle();
        signals[0].Kind.Should().Be(EventWatchSignalKind.TicketAvailable);
        signals[0].WatchId.Should().Be("riga-fc-smoke-kairat-2026");
        signals[0].SourceUrl.Should().Be("https://www.bilesuserviss.lv/biletes/KAIRAT01/riga-fc-vs-kairat-almaty");
        signals[0].TicketUrl.Should().Be(signals[0].SourceUrl);
        signals[0].Evidence.Should().Contain("Kairat");
    }

    [Test]
    public void Detect_AtalantaProduct_ReturnsSingleAtalantaSignal()
    {
        var observations = ParseCatalogue(CatalogueFixtures.WithAtalanta());

        var signals = _detector.Detect(CreateAtalantaWatch(), observations);

        signals.Should().ContainSingle();
        signals[0].Kind.Should().Be(EventWatchSignalKind.TicketAvailable);
        signals[0].WatchId.Should().Be("riga-fc-atalanta-2026");
        signals[0].SourceUrl.Should().Be("https://www.bilesuserviss.lv/biletes/ATALANTA01/riga-fc-vs-atalanta");
        signals[0].TicketUrl.Should().Be(signals[0].SourceUrl);
        signals[0].Evidence.Should().Contain("Atalanta");
    }

    [Test]
    public void Detect_KairatProduct_DoesNotTriggerAtalanta()
    {
        var observations = ParseCatalogue(CatalogueFixtures.WithKairat());

        _detector.Detect(CreateAtalantaWatch(), observations).Should().BeEmpty();
    }

    [Test]
    public void Detect_AtalantaProduct_DoesNotTriggerKairat()
    {
        var observations = ParseCatalogue(CatalogueFixtures.WithAtalanta());

        _detector.Detect(CreateKairatWatch(), observations).Should().BeEmpty();
    }

    [Test]
    public void Detect_CaseInsensitiveOpponent_Matches()
    {
        var observations = ParseCatalogue(CatalogueFixtures.WithAtalanta());

        var watch = CreateAtalantaWatch();
        watch.MatchTerms.Clear();
        watch.MatchTerms.Add("aTaLaNtA");

        var signals = _detector.Detect(watch, observations);

        signals.Should().ContainSingle();
        signals[0].Kind.Should().Be(EventWatchSignalKind.TicketAvailable);
    }

    [Test]
    public void Detect_MultipleMatchingProducts_CollapsesToSingleSignal()
    {
        var json = """{"status":"success","items":[{"id":"K1","status":"ON_SALE","name":"Riga FC vs Kairat Almaty","sluggedName":"riga-kairat-1","venue":{"name":"Skonto stadions"}},{"id":"K2","status":"ON_SALE","name":"Kairat away sector","sluggedName":"kairat-away","venue":{"name":"Skonto stadions"}}]}""";
        var observations = ParseCatalogue(json);

        var signals = _detector.Detect(CreateKairatWatch(), observations);

        // One useful notification per watch, never duplicates from one catalogue.
        signals.Should().ContainSingle();
    }

    [Test]
    public void Detect_NoMatchTerms_NoSignal()
    {
        var observations = ParseCatalogue(CatalogueFixtures.WithKairat());

        var watch = CreateKairatWatch();
        watch.MatchTerms.Clear();

        _detector.Detect(watch, observations).Should().BeEmpty();
    }
}
