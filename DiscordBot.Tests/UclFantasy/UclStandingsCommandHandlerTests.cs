using AwesomeAssertions;
using DiscordBot.Commands;
using DiscordBot.Tests.PremierLeague;
using DiscordBot.UclFantasy;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace DiscordBot.Tests.UclFantasy;

[TestFixture]
public sealed class UclStandingsCommandHandlerTests
{
    [Test]
    public async Task HandleAsync_FirstPage_RendersLeagueIdentityAndRows()
    {
        // Arrange
        var provider = CreateProvider(SnapshotA());
        var interaction = new TestInteraction();
        var handler = CreateHandler(provider);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().ContainSingle();
        var message = interaction.Messages[0];
        message.Should().Contain("Таблица лиги UCL");
        message.Should().Contain("Great Messi League");
        message.Should().Contain("25247707");
        message.Should().Contain("Matchday 2");
        message.Should().Contain("Alpha FC");
        message.Should().Contain("200");
        message.Should().Contain("Страница 1 из");
    }

    [Test]
    public async Task HandleAsync_SecondPage_ShowsRemainingMembers()
    {
        // Arrange: 12 members with a 10-per-page display.
        var members = Enumerable.Range(1, 12)
            .Select(index => new UclLeagueStanding(
                index,
                $"Team {index:D2}",
                $"Manager {index:D2}",
                index,
                index,
                300 - index,
                10))
            .ToList();
        var provider = CreateProvider(new UclLeagueStandings(
            25247707,
            "Great Messi League",
            1,
            2,
            members));
        var interaction = new TestInteraction();
        interaction.SetIntegerOption("page", 2);
        var handler = CreateHandler(provider);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().ContainSingle();
        var message = interaction.Messages[0];
        message.Should().Contain("Team 11");
        message.Should().Contain("Team 12");
        message.Should().NotContain("Team 01");
        message.Should().Contain("Страница 2 из 2");
    }

    [Test]
    public async Task HandleAsync_PageBeyondTotal_ReturnsActionableMessage()
    {
        // Arrange
        var provider = CreateProvider(SnapshotA());
        var interaction = new TestInteraction();
        interaction.SetIntegerOption("page", 5);
        var handler = CreateHandler(provider);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Contain("недоступна");
        provider.CallCount.Should().Be(1);
    }

    [Test]
    public async Task HandleAsync_ApiFailure_ReturnsUnavailableMessage()
    {
        // Arrange
        var provider = CreateProvider(SnapshotA(), throwApiException: true);
        var interaction = new TestInteraction();
        var handler = CreateHandler(provider);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Contain("Таблица UCL сейчас недоступна");
    }

    [Test]
    public async Task HandleAsync_ChangedAndUnchangedSnapshots_BothRenderWithoutBaselineSideEffects()
    {
        // Arrange: the command is stateless; two different snapshots both render.
        var provider = CreateProvider(SnapshotA());
        var handler = CreateHandler(provider);

        // Act
        var firstInteraction = new TestInteraction();
        await handler.HandleAsync(firstInteraction);
        provider.Standings = SnapshotB();
        var secondInteraction = new TestInteraction();
        await handler.HandleAsync(secondInteraction);

        // Assert
        firstInteraction.Messages.Should().ContainSingle();
        secondInteraction.Messages.Should().ContainSingle();
        firstInteraction.Messages[0].Should().Contain("Alpha FC");
        secondInteraction.Messages[0].Should().Contain("Alpha FC");
        secondInteraction.Messages[0].Should().NotBe(firstInteraction.Messages[0]);
        provider.CallCount.Should().Be(2);
    }

    private static UclLeagueStandings SnapshotA()
    {
        return new UclLeagueStandings(
            25247707,
            "Great Messi League",
            1,
            2,
            [
                new UclLeagueStanding(101, "Alpha FC", "Anna", 1, 1, 200, 60),
                new UclLeagueStanding(102, "Beta United", "Boris", 2, 3, 190, 70)
            ]);
    }

    private static UclLeagueStandings SnapshotB()
    {
        return new UclLeagueStandings(
            25247707,
            "Great Messi League",
            1,
            2,
            [
                new UclLeagueStanding(101, "Alpha FC", "Anna", 1, 1, 205, 65),
                new UclLeagueStanding(102, "Beta United", "Boris", 2, 1, 195, 60)
            ]);
    }

    private static UclStandingsCommandHandler CreateHandler(TestProvider provider)
    {
        return new UclStandingsCommandHandler(
            provider.Provider,
            new UclFantasyMessageCompositionService(TestTimeZones.Riga()),
            NullLogger<UclStandingsCommandHandler>.Instance);
    }

    private static TestProvider CreateProvider(
        UclLeagueStandings standings,
        bool throwApiException = false)
    {
        var client = new StubClient(standings, throwApiException);
        var options = new UclFantasyOptions
        {
            LeagueId = 25247707,
            MaxStandingsPages = 10,
            PageSize = 25,
            OptType = 1,
            PhaseId = 1,
            MatchdayId = 2
        };
        var inner = new UclLeagueStandingsProvider(
            client,
            options,
            NullLogger<UclLeagueStandingsProvider>.Instance);
        return new TestProvider(inner, client);
    }

    private sealed class TestProvider(
        UclLeagueStandingsProvider inner,
        StubClient client)
    {
        public UclLeagueStandingsProvider Provider => inner;

        public int CallCount => client.CallCount;

        public UclLeagueStandings Standings
        {
            get => client.Standings;
            set => client.Standings = value;
        }
    }

    private sealed class StubClient(UclLeagueStandings standings, bool throwApiException)
        : IUclFantasyClient
    {
        public UclLeagueStandings Standings { get; set; } = standings;

        public int CallCount { get; private set; }

        public Task<UclFantasyWebConfigurationResponse> GetWebConfigurationAsync(
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<UclFantasyFixturesResponse> GetFixturesAsync(
            UclFantasyWebConfigurationValue webConfiguration,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<UclLeagueStandings> GetLeagueStandingsAsync(
            UclLeagueStandingsQuery query,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (throwApiException)
            {
                throw new UclFantasyApiException(
                    UclFantasyFailureKind.Transient,
                    "Transient test failure.");
            }

            return Task.FromResult(Standings);
        }
    }

    private sealed class TestInteraction : IDiscordSlashCommandInteraction
    {
        private readonly Dictionary<string, long> _integerOptions = new(StringComparer.Ordinal);

        public string Name => DiscordApplicationCommands.UclStandingsName;

        public string UserMention => "<@123>";

        public List<string> Messages { get; } = [];

        public int FollowupCount { get; private set; }

        public void SetIntegerOption(string name, long value) =>
            _integerOptions[name] = value;

        public string? GetStringOption(string name) => null;

        public long? GetIntegerOption(string name) =>
            _integerOptions.TryGetValue(name, out var value) ? value : null;

        public Task RespondAsync(string content)
        {
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task DeferAsync() => Task.CompletedTask;

        public Task ModifyOriginalResponseAsync(string content)
        {
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task FollowupAsync(string content)
        {
            FollowupCount++;
            Messages.Add(content);
            return Task.CompletedTask;
        }
    }
}
