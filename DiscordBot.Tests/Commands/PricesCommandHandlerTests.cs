using System.Net;
using AwesomeAssertions;
using DiscordBot.Commands;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.PriceChanges;
using DiscordBot.Responses;
using NUnit.Framework;

namespace DiscordBot.Tests.Commands;

[TestFixture]
public sealed class PricesCommandHandlerTests
{
    [Test]
    public async Task HandleAsync_CurrentChanges_ReturnsLeagueAwareReportWithoutAdvancingSnapshot()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = CreateBootstrap(101),
            Managers = [new ClassicStanding { Entry = 1, EntryName = "Bobrov FC" }]
        };
        client.Picks[1] = new EntryEventPicksResponse
        {
            Picks = [new EntryEventPick { Element = 10 }]
        };
        var store = new InMemoryPriceStore();
        store.SaveSnapshot(new Dictionary<int, int> { [10] = 100 });
        var handler = CreateHandler(client, store);
        var interaction = new TestInteraction();

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Operations.Should().StartWith("Defer");
        interaction.Messages.Should().ContainSingle()
            .Which.Should().Contain("В составах: Bobrov FC");
        store.GetSnapshot()!.Prices[10].Should().Be(100);
        store.GetSnapshot()!.Version.Should().Be(1);
    }

    [Test]
    public async Task HandleAsync_SavedBatchFromPreviousGameweek_UsesSavedGameweekSquads()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = CreateBootstrap(101, eventId: 4),
            Managers = [new ClassicStanding { Entry = 1, EntryName = "Bobrov FC" }]
        };
        client.Picks[1] = new EntryEventPicksResponse
        {
            Picks = [new EntryEventPick { Element = 10 }]
        };
        var store = new InMemoryPriceStore();
        store.SaveSnapshot(new FplPriceChangeCheck(
            new Dictionary<int, int> { [10] = 101 },
            [new FplPlayerPriceChange(10, "Salah", 100, 101)],
            PreviousSnapshotVersion: 1,
            CheckedAtUtc: DateTimeOffset.Parse("2026-08-30T10:00:00Z"),
            CurrentEventId: 3));
        var handler = CreateHandler(client, store);
        var interaction = new TestInteraction();

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().ContainSingle()
            .Which.Should().StartWith("💰 Последние зафиксированные");
        client.RequestedPickEventIds.Should().Equal(3);
    }

    [Test]
    public async Task HandleAsync_FplUnavailable_ReturnsRetryMessage()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            BootstrapException = new FantasyPremierLeagueApiException(
                FantasyPremierLeagueFailureKind.Transient,
                "Unavailable.",
                HttpStatusCode.ServiceUnavailable)
        };
        var handler = CreateHandler(client, new InMemoryPriceStore());
        var interaction = new TestInteraction();

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().Equal(
            "⚠️ Изменения цен FPL сейчас недоступны. Попробуйте ещё раз позже.");
    }

    private static PricesCommandHandler CreateHandler(
        TestFantasyPremierLeagueClient client,
        InMemoryPriceStore store)
    {
        return new PricesCommandHandler(
            new FplPriceChangeService(
                client,
                store,
                TimeProvider.System,
                new RecordingLogger<FplPriceChangeService>()),
            new FplLeaguePriceChangeService(
                client,
                new FantasyPremierLeagueOptions { ClassicLeagueId = 123 },
                new RecordingLogger<FplLeaguePriceChangeService>()),
            new FplPriceChangeMessageCompositionService(),
            new RecordingLogger<PricesCommandHandler>());
    }

    private static BootstrapStaticResponse CreateBootstrap(
        int cost,
        int eventId = 3)
    {
        return new BootstrapStaticResponse
        {
            Events =
            [
                new PremierLeagueEvent
                {
                    Id = eventId,
                    IsCurrent = true
                }
            ],
            Elements =
            [
                new PremierLeagueElement
                {
                    Id = 10,
                    WebName = "Salah",
                    NowCost = cost
                }
            ]
        };
    }

    private sealed class TestFantasyPremierLeagueClient : IFantasyPremierLeagueClient
    {
        public BootstrapStaticResponse Bootstrap { get; init; } = CreateBootstrap(100);

        public Exception? BootstrapException { get; init; }

        public List<ClassicStanding> Managers { get; init; } = [];

        public Dictionary<int, EntryEventPicksResponse> Picks { get; } = [];

        public List<int> RequestedPickEventIds { get; } = [];

        public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
            CancellationToken cancellationToken)
        {
            return BootstrapException is null
                ? Task.FromResult(Bootstrap)
                : Task.FromException<BootstrapStaticResponse>(BootstrapException);
        }

        public Task<ClassicStandingsResponse> GetClassicStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ClassicStandingsResponse
            {
                Standings = new ClassicStandings { Results = Managers }
            });
        }

        public Task<EntryEventPicksResponse> GetEntryEventPicksAsync(
            int entryId,
            int eventId,
            CancellationToken cancellationToken)
        {
            RequestedPickEventIds.Add(eventId);
            return Task.FromResult(Picks[entryId]);
        }

        public Task<HeadToHeadStandingsResponse> GetHeadToHeadStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EventLiveResponse> GetEventLiveAsync(
            int eventId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<PremierLeagueFixture>> GetFixturesAsync(
            int eventId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class InMemoryPriceStore : IFplPriceSnapshotStore
    {
        private Dictionary<int, int> _prices = [];
        private long _version;
        private FplPriceChangeBatch? _latestChanges;

        public FplPriceSnapshot? GetSnapshot() => _version == 0
            ? null
            : new FplPriceSnapshot(_version, new Dictionary<int, int>(_prices));

        public FplPriceChangeBatch? GetLatestChanges() => _latestChanges;

        public void SaveSnapshot(IReadOnlyDictionary<int, int> prices)
        {
            _prices = new Dictionary<int, int>(prices);
            _version++;
        }

        public void SaveSnapshot(FplPriceChangeCheck priceCheck)
        {
            SaveSnapshot(priceCheck.CurrentPrices);
            if (priceCheck.Changes.Count > 0)
            {
                _latestChanges = new FplPriceChangeBatch(
                    priceCheck.CheckedAtUtc,
                    priceCheck.CurrentEventId,
                    priceCheck.Changes);
            }
        }
    }

    private sealed class TestInteraction : IDiscordSlashCommandInteraction
    {
        public string Name => DiscordApplicationCommands.PricesName;

        public string UserMention => "<@1>";

        public List<string> Messages { get; } = [];

        public List<string> Operations { get; } = [];

        public string? GetStringOption(string name) => null;

        public long? GetIntegerOption(string name) => null;

        public Task RespondAsync(string content)
        {
            Operations.Add("Respond");
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task DeferAsync()
        {
            Operations.Add("Defer");
            return Task.CompletedTask;
        }

        public Task ModifyOriginalResponseAsync(string content)
        {
            Operations.Add("Modify");
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task FollowupAsync(string content)
        {
            Operations.Add("Followup");
            Messages.Add(content);
            return Task.CompletedTask;
        }
    }
}
