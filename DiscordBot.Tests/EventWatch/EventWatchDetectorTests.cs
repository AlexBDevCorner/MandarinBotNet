using AwesomeAssertions;
using DiscordBot;
using DiscordBot.EventWatch;
using NUnit.Framework;

namespace DiscordBot.Tests.EventWatch;

[TestFixture]
public sealed class EventWatchDetectorTests
{
    private static readonly Uri Homepage = new("https://rigafc.lv/");
    private static readonly Uri Calendar = new("https://rigafc.lv/kalendars/");
    private static readonly Uri News = new("https://rigafc.lv/jaunumi/");

    private RigaFcPageParser _parser = null!;
    private EventWatchSignalDetector _detector = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new RigaFcPageParser();
        _detector = new EventWatchSignalDetector();
    }

    private static DiscordBot.EventWatchDefinition CreateWatch()
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

    private IReadOnlyList<EventWatchObservation> Parse(string html, Uri source)
    {
        return _parser.Parse(html, source);
    }

    [Test]
    public void Detect_AtalantaAbsent_NoSignal()
    {
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Valmiera</h2><p>Biļetes jau pārdošanā! Pērciet biļetes tagad.</p></article></div>
            </body></html>
            """;
        var observations = Parse(html, Homepage);

        var signals = _detector.Detect(CreateWatch(), observations);

        signals.Should().BeEmpty();
    }

    [Test]
    public void Detect_AtalantaWithoutTicketWording_NoSignal()
    {
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Fixture on Saturday at Skonto Stadium at 17:00.</p></article></div>
            </body></html>
            """;
        var observations = Parse(html, Homepage);

        var signals = _detector.Detect(CreateWatch(), observations);

        signals.Should().BeEmpty();
    }

    [Test]
    public void Detect_AtalantaPlusGenericNavigationBiletes_NoSignal()
    {
        var html = """
            <html><body>
            <nav><a href="/biletes">Biļetes</a></nav>
            <header><a href="/tickets">Tickets</a></header>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Fixture on Saturday at Skonto Stadium.</p></article></div>
            <footer><a href="/shop">Biļetes</a></footer>
            </body></html>
            """;
        var observations = Parse(html, Homepage);

        var signals = _detector.Detect(CreateWatch(), observations);

        signals.Should().BeEmpty();
    }

    [Test]
    public void Detect_SaleAnnouncement_ReturnsAnnouncement()
    {
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Biļetes jau pārdošanā! Iegādāties biļetes uz šo spēli.</p></article></div>
            </body></html>
            """;
        var observations = Parse(html, Homepage);

        var signals = _detector.Detect(CreateWatch(), observations);

        signals.Should().ContainSingle();
        signals[0].Kind.Should().Be(EventWatchSignalKind.Announcement);
        signals[0].WatchId.Should().Be("riga-fc-atalanta-2026");
        signals[0].SourceUrl.Should().Be(Homepage.ToString());
    }

    [Test]
    public void Detect_AssociatedTicketLink_ReturnsTicketLink()
    {
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Ticket info for Riga FC vs Atalanta.</p><a href="https://bilesuserviss.lv/riga-atalanta">Pirkt biļetes</a></article></div>
            </body></html>
            """;
        var observations = Parse(html, Homepage);

        var signals = _detector.Detect(CreateWatch(), observations);

        // Same observation proves both announcement (sale wording) and ticket link.
        signals.Should().HaveCount(2);
        signals.Should().Contain(s => s.Kind == EventWatchSignalKind.Announcement);
        var ticket = signals.Should().Contain(s => s.Kind == EventWatchSignalKind.TicketLinkAvailable).Subject;
        ticket.TicketUrl.Should().Be("https://bilesuserviss.lv/riga-atalanta");
        ticket.SourceUrl.Should().Be(Homepage.ToString());
    }

    [Test]
    public void Detect_TicketLinkWithUrlCueOnly_ReturnsTicketLinkWithoutAnnouncement()
    {
        // Anchor URL looks like a ticket shop but surrounding text has no
        // sale vocabulary except the URL cue; match context still applies.
        // To isolate the ticket-link path, the visible text contains the
        // match term plus a ticket URL cue in href, but no sale words.
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Match centre for Riga FC vs Atalanta.</p><a href="https://bilesuserviss.lv/riga-atalanta">Read more</a></article></div>
            </body></html>
            """;
        var observations = Parse(html, Homepage);

        var signals = _detector.Detect(CreateWatch(), observations);

        // "tickets" is not in text; URL cue alone should still yield a ticket link
        // only if the surrounding context mentions the match. The visible text
        // "Match centre" has no sale wording, so only the ticket link fires.
        signals.Should().ContainSingle();
        signals[0].Kind.Should().Be(EventWatchSignalKind.TicketLinkAvailable);
    }

    [Test]
    public void Detect_AnnouncementPlusTicketLink_ReturnsBoth()
    {
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Biļetes pārdošanā uz Riga FC vs Atalanta spēli.</p><a href="https://bilesuserviss.lv/xyz">Pirkt biļetes</a></article></div>
            </body></html>
            """;
        var observations = Parse(html, Homepage);

        var signals = _detector.Detect(CreateWatch(), observations);

        signals.Should().HaveCount(2);
        signals.Should().Contain(s => s.Kind == EventWatchSignalKind.Announcement);
        signals.Should().Contain(s => s.Kind == EventWatchSignalKind.TicketLinkAvailable);
    }

    [Test]
    public void Detect_CaseInsensitiveAtalanta_Matches()
    {
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs ATALANTA</h2><p>BIĻETES PĀRDOŠANĀ!</p></article></div>
            </body></html>
            """;
        var observations = Parse(html, Homepage);

        var watch = CreateWatch();
        watch.MatchTerms.Clear();
        watch.MatchTerms.Add("aTaLaNtA");

        var signals = _detector.Detect(watch, observations);

        signals.Should().NotBeEmpty();
        signals.Should().Contain(s => s.Kind == EventWatchSignalKind.Announcement);
    }

    [Test]
    public void Detect_DuplicateObservationsAcrossPages_CollapsesToOnePerKind()
    {
        var htmlTemplate = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Biļetes pārdošanā!</p><a href="https://bilesuserviss.lv/dup">Pirkt biļetes</a></article></div>
            </body></html>
            """;
        var homepage = Parse(htmlTemplate, Homepage);
        var calendar = Parse(htmlTemplate, Calendar);
        var news = Parse(htmlTemplate, News);
        var combined = homepage.Concat(calendar).Concat(news).ToList();

        var signals = _detector.Detect(CreateWatch(), combined);

        signals.Should().HaveCount(2);
        signals.Where(s => s.Kind == EventWatchSignalKind.Announcement).Should().ContainSingle();
        signals.Where(s => s.Kind == EventWatchSignalKind.TicketLinkAvailable).Should().ContainSingle();
    }

    [Test]
    public void Parse_RelativeLink_NormalizesToAbsolute()
    {
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Biļetes pārdošanā.</p><a href="/biletes/riga-atalanta">Pirkt</a></article></div>
            </body></html>
            """;
        var observations = Parse(html, Homepage);

        observations.Should().NotBeEmpty();
        var withLink = observations.FirstOrDefault(o => o.Anchors.Count > 0);
        withLink.Should().NotBeNull();
        withLink!.Anchors[0].Url.Should().Be("https://rigafc.lv/biletes/riga-atalanta");
    }

    [Test]
    public void Parse_AbsoluteLink_Preserved()
    {
        var html = """
            <html><body>
            <div><article><h2>News</h2><p>Text.</p><a href="https://bilesuserviss.lv/game">Tickets</a></article></div>
            </body></html>
            """;
        var observations = Parse(html, News);

        var withLink = observations.FirstOrDefault(o => o.Anchors.Count > 0);
        withLink.Should().NotBeNull();
        withLink!.Anchors[0].Url.Should().Be("https://bilesuserviss.lv/game");
    }

    [Test]
    public void Parse_ExposesSourceUrlTextAnchorAndContext()
    {
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta preview</h2><p>Biļetes pārdošanā uz šo spēli.</p><a href="https://bilesuserviss.lv/x">Pirkt biļetes</a></article></div>
            </body></html>
            """;
        var observations = Parse(html, Calendar);

        observations.Should().NotBeEmpty();
        var observation = observations.First(o => o.Anchors.Count > 0);
        observation.SourceUrl.Should().Be(Calendar.ToString());
        observation.Text.Should().Contain("Atalanta");
        observation.Anchors[0].Text.Should().Contain("Pirkt");
        observation.Anchors[0].Url.Should().StartWith("https://");
        observation.Context.Should().NotBeNull();
    }
}

