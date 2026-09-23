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
    private const string KairatWatchId = "riga-fc-smoke-kairat-2026";

    [Test]
    public async Task GetStatusAsync_NoOpponentProduct_ReturnsHealthyNoSignal()
    {
        var observations = new RigaFcTicketCatalogueParser().Parse(
            CatalogueFixtures.GenericPackage(),
            RigaFcClient.TicketCatalogueApiUri);
        var service = CreateService(
            [CreateAtalantaWatch(), CreateKairatWatch()],
            new FixedSource(observations),
            schedulingEnabled: true);

        var report = await service.GetStatusAsync(CancellationToken.None);

        report.CollectionSucceeded.Should().BeTrue();
        report.SchedulingEnabled.Should().BeTrue();
        report.ObservationCount.Should().Be(2);
        report.EnabledWatches.Should().HaveCount(2);
        report.SourcePages.Should().ContainSingle()
            .Which.Should().Be(RigaFcClient.TicketCatalogueApiUri.ToString());
        report.SourcePages.Should().NotContain("https://rigafc.lv/kalendars/");
        report.HasSignals.Should().BeFalse();
        report.WatchResults.Should().HaveCount(2);
        report.WatchResults.Should().OnlyContain(r => r.TicketAvailableCount == 0);

        var message = new EventWatchStatusMessageComposer().Compose(report);
        message.Should().Contain("Healthy");
        message.Should().Contain("catalogue reachable");
        message.Should().Contain("no opponent tickets");
        message.Should().Contain("0 ticket available");
        message.Should().NotContain("{");
        message.Should().NotContain("@everyone");
    }

    [Test]
    public async Task GetStatusAsync_MatchingProduct_ReportsDetectedMatch()
    {
        var observations = new RigaFcTicketCatalogueParser().Parse(
            CatalogueFixtures.WithKairat(),
            RigaFcClient.TicketCatalogueApiUri);
        var service = CreateService(
            [CreateAtalantaWatch(), CreateKairatWatch()],
            new FixedSource(observations),
            schedulingEnabled: true);

        var report = await service.GetStatusAsync(CancellationToken.None);

        report.CollectionSucceeded.Should().BeTrue();
        report.HasSignals.Should().BeTrue();
        var kairat = report.WatchResults.Should().ContainSingle(r => r.WatchId == KairatWatchId).Subject;
        kairat.TicketAvailableCount.Should().Be(1);
        kairat.EvidenceSourceUrls.Should().Contain("https://www.bilesuserviss.lv/biletes/KAIRAT01/riga-fc-vs-kairat-almaty");
        var atalanta = report.WatchResults.Should().ContainSingle(r => r.WatchId == AtalantaWatchId).Subject;
        atalanta.TicketAvailableCount.Should().Be(0);

        var message = new EventWatchStatusMessageComposer().Compose(report);
        message.Should().Contain("1 ticket available");
        message.Should().Contain("KAIRAT01");
        message.Should().Contain("Opponent tickets detected");
        message.Should().NotContain("{");
        message.Should().NotContain("@everyone");

        // Status is read-only: a second unchanged evaluation reports the same
        // signals without any delivery side effect (no publisher involved).
        var second = await service.GetStatusAsync(CancellationToken.None);
        second.HasSignals.Should().BeTrue();
        second.WatchResults.Single(r => r.WatchId == KairatWatchId).TicketAvailableCount.Should().Be(1);
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
        message.Should().Contain("Catalogue: failed");
        message.Should().NotContain("{");
    }

    [Test]
    public async Task GetStatusAsync_SchedulingDisabled_StillReportsCollection()
    {
        var observations = new RigaFcTicketCatalogueParser().Parse(
            CatalogueFixtures.GenericPackage(),
            RigaFcClient.TicketCatalogueApiUri);
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

    private static EventWatchDefinition CreateKairatWatch()
    {
        return new EventWatchDefinition
        {
            Enabled = true,
            Id = KairatWatchId,
            Title = "Riga FC vs Kairat tickets",
            MatchTerms = ["Kairat"],
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
                new InvalidOperationException("Riga FC ticket catalogue fetch failed."));
    }
}
