using AwesomeAssertions;
using DiscordBot.EventWatch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace DiscordBot.Tests.EventWatch;

[TestFixture]
public sealed class EventWatchStatusServiceTests
{
    private const string AtalantaWatchId = "riga-fc-atalanta-2026";

    [Test]
    public async Task GetStatusAsync_NoSale_ReturnsHealthyNoSignal()
    {
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Fixture on Saturday at Skonto Stadium at 17:00.</p></article></div>
            </body></html>
            """;
        var observations = new RigaFcPageParser().Parse(html, new Uri("https://rigafc.lv/"));
        var service = CreateService([CreateAtalantaWatch()], new FixedSource(observations), schedulingEnabled: true);

        var report = await service.GetStatusAsync(CancellationToken.None);

        report.CollectionSucceeded.Should().BeTrue();
        report.SchedulingEnabled.Should().BeTrue();
        report.ObservationCount.Should().BeGreaterThan(0);
        report.EnabledWatches.Should().ContainSingle().Which.Id.Should().Be(AtalantaWatchId);
        report.SourcePages.Should().HaveCount(3);
        report.HasSignals.Should().BeFalse();
        var watchResult = report.WatchResults.Should().ContainSingle().Subject;
        watchResult.AnnouncementCount.Should().Be(0);
        watchResult.TicketLinkCount.Should().Be(0);
        watchResult.MatchTerms.Should().Equal("Atalanta");

        var message = new EventWatchStatusMessageComposer().Compose(report);
        message.Should().Contain("Healthy");
        message.Should().Contain("no qualifying signals");
        message.Should().NotContain("<html>");
        message.Should().NotContain("@everyone");
    }

    [Test]
    public async Task GetStatusAsync_MatchingFixture_ReportsDetectedSignalKinds()
    {
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Biļetes pārdošanā uz Riga FC vs Atalanta spēli.</p><a href="https://bilesuserviss.lv/riga-atalanta">Pirkt biļetes</a></article></div>
            </body></html>
            """;
        var observations = new RigaFcPageParser().Parse(html, new Uri("https://rigafc.lv/"));
        var service = CreateService([CreateAtalantaWatch()], new FixedSource(observations), schedulingEnabled: true);

        var report = await service.GetStatusAsync(CancellationToken.None);

        report.CollectionSucceeded.Should().BeTrue();
        report.HasSignals.Should().BeTrue();
        var watchResult = report.WatchResults.Should().ContainSingle().Subject;
        watchResult.AnnouncementCount.Should().Be(1);
        watchResult.TicketLinkCount.Should().Be(1);
        watchResult.EvidenceSourceUrls.Should().Contain("https://rigafc.lv/");

        var message = new EventWatchStatusMessageComposer().Compose(report);
        message.Should().Contain("1 announcement");
        message.Should().Contain("1 ticket-link");
        message.Should().Contain("https://rigafc.lv/");
        message.Should().NotContain("<html>");
        message.Should().NotContain("@everyone");

        // Status is read-only: a second unchanged evaluation reports the same
        // signals without any delivery side effect (no publisher involved).
        var second = await service.GetStatusAsync(CancellationToken.None);
        second.HasSignals.Should().BeTrue();
        second.WatchResults[0].AnnouncementCount.Should().Be(1);
        second.WatchResults[0].TicketLinkCount.Should().Be(1);
    }

    [Test]
    public async Task GetStatusAsync_SourceThrows_ReportsFailureDistinctly()
    {
        var service = CreateService(
            [CreateAtalantaWatch()],
            new ThrowingSource(),
            schedulingEnabled: true);

        var report = await service.GetStatusAsync(CancellationToken.None);

        report.CollectionSucceeded.Should().BeFalse();
        report.FailureReason.Should().NotBeNullOrWhiteSpace();
        report.ObservationCount.Should().Be(0);
        report.WatchResults.Should().BeEmpty();
        report.HasSignals.Should().BeFalse();

        var message = new EventWatchStatusMessageComposer().Compose(report);
        message.Should().Contain("failed");
        message.Should().Contain("Collection: failed");
        message.Should().NotContain("<html>");
    }

    [Test]
    public async Task GetStatusAsync_SchedulingDisabled_StillReportsCollection()
    {
        var html = """
            <html><body>
            <div><article><h2>Riga FC vs Atalanta</h2><p>Fixture on Saturday.</p></article></div>
            </body></html>
            """;
        var observations = new RigaFcPageParser().Parse(html, new Uri("https://rigafc.lv/"));
        var service = CreateService([CreateAtalantaWatch()], new FixedSource(observations), schedulingEnabled: false);

        var report = await service.GetStatusAsync(CancellationToken.None);

        report.SchedulingEnabled.Should().BeFalse();
        report.CollectionSucceeded.Should().BeTrue();
        var message = new EventWatchStatusMessageComposer().Compose(report);
        message.Should().Contain("disabled");
    }

    private static EventWatchDefinition CreateAtalantaWatch()
    {
        return new EventWatchDefinition
        {
            Enabled = true,
            Id = AtalantaWatchId,
            Title = "Riga FC vs Atalanta tickets",
            MatchTerms = ["Atalanta"],
            Targets = [new NotificationTargetOptions { GuildId = 10, ChannelId = 100 }]
        };
    }

    private static EventWatchStatusService CreateService(
        IReadOnlyList<EventWatchDefinition> watches,
        IEventWatchSource source,
        bool schedulingEnabled)
    {
        var botOptions = new MandarinBotOptions
        {
            Schedules = new JobSchedulesOptions
            {
                EventWatch = new ScheduledJobOptions
                {
                    Enabled = schedulingEnabled,
                    Cron = "0 0/10 * * * ?"
                }
            },
            EventWatch = new EventWatchOptions { Watches = watches.ToList() }
        };
        return new EventWatchStatusService(
            source,
            new EventWatchSignalDetector(),
            Options.Create(botOptions),
            new RecordingLogger<EventWatchStatusService>());
    }

    private sealed class FixedSource(IReadOnlyList<EventWatchObservation> observations) : IEventWatchSource
    {
        public string SourceName => "test";

        public Task<IReadOnlyList<EventWatchObservation>> CollectAsync(CancellationToken cancellationToken) =>
            Task.FromResult(observations);
    }

    private sealed class ThrowingSource : IEventWatchSource
    {
        public string SourceName => "test";

        public Task<IReadOnlyList<EventWatchObservation>> CollectAsync(CancellationToken cancellationToken) =>
            Task.FromException<IReadOnlyList<EventWatchObservation>>(
                new InvalidOperationException("Riga FC fetch failed."));
    }
}
