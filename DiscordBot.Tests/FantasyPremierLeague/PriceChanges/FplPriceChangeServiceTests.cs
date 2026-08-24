using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.PriceChanges;
using DiscordBot.Responses;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.PriceChanges;

[TestFixture]
public sealed class FplPriceChangeServiceTests
{
    [Test]
    public async Task CheckAsync_FirstSnapshot_ReturnsNoChanges()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = CreateBootstrap((1, "Salah", 100), (2, "Haaland", 150))
        };
        var store = new InMemoryFplPriceSnapshotStore();
        var service = new FplPriceChangeService(
            client,
            store,
            new RecordingLogger<FplPriceChangeService>());

        // Act
        var result = await service.CheckAsync(CancellationToken.None);

        // Assert
        result.Changes.Should().BeEmpty();
        result.CurrentPrices.Should().BeEquivalentTo(
            new Dictionary<int, int>
            {
                [1] = 100,
                [2] = 150
            });
        store.Prices.Should().BeEmpty();
    }

    [Test]
    public async Task CheckAsync_ChangedCurrentPrices_ReturnsOnlyChangedPlayers()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = CreateBootstrap(
                (1, "Salah", 101),
                (2, "Haaland", 150),
                (3, "Saka", 99))
        };
        var store = new InMemoryFplPriceSnapshotStore();
        store.SaveSnapshot(
            new Dictionary<int, int>
            {
                [1] = 100,
                [2] = 150,
                [3] = 100
            });
        var service = new FplPriceChangeService(
            client,
            store,
            new RecordingLogger<FplPriceChangeService>());

        // Act
        var result = await service.CheckAsync(CancellationToken.None);

        // Assert
        result.Changes.Should().BeEquivalentTo(
            [
                new FplPlayerPriceChange(3, "Saka", 100, 99),
                new FplPlayerPriceChange(1, "Salah", 100, 101)
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public async Task SaveSnapshot_AfterPriceChange_MakesNextCheckQuiet()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = CreateBootstrap((1, "Salah", 101))
        };
        var store = new InMemoryFplPriceSnapshotStore();
        store.SaveSnapshot(new Dictionary<int, int> { [1] = 100 });
        var service = new FplPriceChangeService(
            client,
            store,
            new RecordingLogger<FplPriceChangeService>());
        var changedCheck = await service.CheckAsync(CancellationToken.None);
        service.SaveSnapshot(changedCheck);

        // Act
        var result = await service.CheckAsync(CancellationToken.None);

        // Assert
        result.Changes.Should().BeEmpty();
        store.Prices.Should().BeEquivalentTo(new Dictionary<int, int> { [1] = 101 });
    }

    [Test]
    public async Task CheckAsync_IncompletePlayerPriceData_ThrowsWithoutChangingSnapshot()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = CreateBootstrap((1, "Salah", 0))
        };
        var store = new InMemoryFplPriceSnapshotStore();
        store.SaveSnapshot(new Dictionary<int, int> { [1] = 100 });
        var service = new FplPriceChangeService(
            client,
            store,
            new RecordingLogger<FplPriceChangeService>());

        // Act
        Func<Task> act = () => service.CheckAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
        store.Prices.Should().BeEquivalentTo(new Dictionary<int, int> { [1] = 100 });
    }

    [Test]
    public void FplPriceChangeSourceIdentifier_SnapshotVersions_DistinguishRepeatedTransitions()
    {
        // Arrange
        var change = new FplPlayerPriceChange(1, "Salah", 100, 101);

        // Act
        var firstIdentifier = FplPriceChangeSourceIdentifier.Create(1, [change]);
        var repeatedIdentifier = FplPriceChangeSourceIdentifier.Create(2, [change]);

        // Assert
        firstIdentifier.Should().NotBe(repeatedIdentifier);
        FplPriceChangeSourceIdentifier.Create(1, [change])
            .Should().Be(firstIdentifier);
    }

    private static BootstrapStaticResponse CreateBootstrap(
        params (int Id, string Name, int Cost)[] players)
    {
        return new BootstrapStaticResponse
        {
            Elements = players
                .Select(player => new PremierLeagueElement
                {
                    Id = player.Id,
                    WebName = player.Name,
                    NowCost = player.Cost
                })
                .ToList()
        };
    }

    private sealed class TestFantasyPremierLeagueClient : IFantasyPremierLeagueClient
    {
        public BootstrapStaticResponse Bootstrap { get; set; } = CreateBootstrap(
            (1, "Salah", 100));

        public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Bootstrap);
        }

        public Task<ClassicStandingsResponse> GetClassicStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<HeadToHeadStandingsResponse> GetHeadToHeadStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<EntryEventPicksResponse> GetEntryEventPicksAsync(
            int entryId,
            int eventId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<EventLiveResponse> GetEventLiveAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class InMemoryFplPriceSnapshotStore : IFplPriceSnapshotStore
    {
        public Dictionary<int, int> Prices { get; } = [];

        public FplPriceSnapshot? GetSnapshot()
        {
            return Version == 0
                ? null
                : new FplPriceSnapshot(Version, new Dictionary<int, int>(Prices));
        }

        public long Version { get; private set; }

        public void SaveSnapshot(IReadOnlyDictionary<int, int> prices)
        {
            Prices.Clear();
            foreach (var price in prices)
            {
                Prices.Add(price.Key, price.Value);
            }

            Version++;
        }
    }
}
