using AwesomeAssertions;
using DiscordBot.BenchWarming;
using DiscordBot.Commands;
using NUnit.Framework;

namespace DiscordBot.Tests.Commands;

[TestFixture]
public sealed class BenchLeagueCommandHandlerTests
{
    private readonly BenchWarmingMessageComposer _composer = new();

    [Test]
    public async Task HandleAsync_NoArguments_CallsSeasonOverview()
    {
        // Arrange
        var overview = new BenchWarmingSeasonOverview(
            "2026/27",
            new BenchWarmingTrackingInfo(2, 5, 4),
            [new BenchWarmingEntryStanding(100, "Team A", 42)],
            5,
            [new BenchWarmingEntryStanding(100, "Team A", 17)],
            new BenchWarmingSeasonRecords(null, 0, null, null));
        var query = new TestQueryService { Overview = overview };
        var handler = new BenchLeagueCommandHandler(query, _composer);
        var interaction = new TestSlashCommandInteraction();

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        query.OverviewCalls.Should().Be(1);
        query.RoundCalls.Should().Be(0);
        query.TeamCalls.Should().Be(0);
        interaction.Operations.Should().Equal("Defer", "Modify");
        interaction.Messages.Should().Equal(_composer.ComposeSeasonOverview(overview));
    }

    [Test]
    public async Task HandleAsync_NoData_ShowsUnavailableMessage()
    {
        // Arrange
        var query = new TestQueryService { Overview = null };
        var handler = new BenchLeagueCommandHandler(query, _composer);
        var interaction = new TestSlashCommandInteraction();

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().Equal(
            "🔥 Лига обогревателей скамейки пока недоступна. Попробуйте ещё раз позже.");
    }

    [Test]
    public async Task HandleAsync_GwOption_CallsRoundQuery()
    {
        // Arrange
        var round = BenchWarmingRoundSummaryResult.ForAvailable(
            new BenchWarmingRoundSummary(
                "2026/27",
                5,
                [new BenchWarmingEntryStanding(100, "Team A", 17)],
                null));
        var query = new TestQueryService { RoundResult = round };
        var handler = new BenchLeagueCommandHandler(query, _composer);
        var interaction = new TestSlashCommandInteraction { Gw = 5 };

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        query.RoundCalls.Should().Be(1);
        query.RoundArgument.Should().Be(5);
        query.TeamCalls.Should().Be(0);
        interaction.Messages.Should().Equal(_composer.ComposeRoundStandings(round.Summary!));
    }

    [Test]
    public async Task HandleAsync_TeamOption_CallsTeamQuery()
    {
        // Arrange
        var team = BenchWarmingTeamLookupResult.ForAvailable(
            new BenchWarmingTeamProfile(
                "2026/27",
                100,
                "Team A",
                2,
                47,
                9.4,
                18,
                4,
                3,
                2,
                [new BenchWarmingEntryRoundStanding(2, 100, "Team A", 6)]));
        var query = new TestQueryService { TeamResult = team };
        var handler = new BenchLeagueCommandHandler(query, _composer);
        var interaction = new TestSlashCommandInteraction { Team = "Team A" };

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        query.TeamCalls.Should().Be(1);
        query.TeamArgument.Should().Be("Team A");
        query.RoundCalls.Should().Be(0);
        interaction.Messages.Should().Equal(_composer.ComposeTeamProfile(team.Profile!));
    }

    [Test]
    public async Task HandleAsync_BothOptions_ReturnsValidationMessage()
    {
        // Arrange
        var query = new TestQueryService
        {
            Overview = new BenchWarmingSeasonOverview(
                "2026/27",
                new BenchWarmingTrackingInfo(2, 5, 4),
                [],
                5,
                [],
                new BenchWarmingSeasonRecords(null, 0, null, null))
        };
        var handler = new BenchLeagueCommandHandler(query, _composer);
        var interaction = new TestSlashCommandInteraction { Team = "Team A", Gw = 5 };

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        query.OverviewCalls.Should().Be(0);
        query.RoundCalls.Should().Be(0);
        query.TeamCalls.Should().Be(0);
        interaction.Messages.Should().Equal(
            "Используйте либо `team`, либо `gw`, но не оба параметра одновременно.");
    }

    [Test]
    public async Task HandleAsync_LongResponse_UsesFollowupsForChunking()
    {
        // Arrange
        var manyStandings = Enumerable
            .Range(1, 150)
            .Select(i => new BenchWarmingEntryStanding(i, $"Team {i}", i))
            .ToArray();
        var overview = new BenchWarmingSeasonOverview(
            "2026/27",
            new BenchWarmingTrackingInfo(1, 1, 1),
            manyStandings,
            1,
            manyStandings,
            new BenchWarmingSeasonRecords(null, 0, null, null));
        var query = new TestQueryService { Overview = overview };
        var handler = new BenchLeagueCommandHandler(query, _composer);
        var interaction = new TestSlashCommandInteraction();

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Operations[0].Should().Be("Defer");
        interaction.Operations[1].Should().Be("Modify");
        interaction.Operations.Count(o => o == "Followup").Should().BeGreaterThan(0);
    }

    private sealed class TestQueryService : BenchWarmingQueryService
    {
        public BenchWarmingSeasonOverview? Overview { get; init; }

        public BenchWarmingRoundSummaryResult RoundResult { get; init; } =
            BenchWarmingRoundSummaryResult.ForNoData();

        public BenchWarmingTeamLookupResult TeamResult { get; init; } =
            BenchWarmingTeamLookupResult.ForNoData();

        public int OverviewCalls { get; private set; }

        public int RoundCalls { get; private set; }

        public int TeamCalls { get; private set; }

        public int RoundArgument { get; private set; }

        public string? TeamArgument { get; private set; }

        public TestQueryService()
            : base(new FakeStore())
        {
        }

        public override BenchWarmingSeasonOverview? GetSeasonOverview()
        {
            OverviewCalls++;
            return Overview;
        }

        public override BenchWarmingRoundSummaryResult GetRound(int eventId)
        {
            RoundCalls++;
            RoundArgument = eventId;
            return RoundResult;
        }

        public override BenchWarmingTeamLookupResult GetTeam(string query)
        {
            TeamCalls++;
            TeamArgument = query;
            return TeamResult;
        }
    }

    private sealed class FakeStore : IBenchWarmingLeagueStore
    {
        public bool IsRoundCalculated(string season, int eventId) =>
            throw new NotSupportedException();

        public void SaveRound(
            string season,
            int eventId,
            IReadOnlyList<BenchWarmingPlayerPoints> benchPoints,
            DateTimeOffset calculatedAtUtc) =>
            throw new NotSupportedException();

        public IReadOnlyList<BenchWarmingEntryStanding> GetSeasonStandings(string season) =>
            throw new NotSupportedException();

        public IReadOnlyList<BenchWarmingEntryRoundStanding> GetSeasonRoundStandings(
            string season) => throw new NotSupportedException();

        public BenchWarmingTrackingInfo? GetTrackingInfo(string season) =>
            throw new NotSupportedException();

        public IReadOnlyList<BenchWarmingPlayerPoints> GetRoundBenchPoints(
            string season,
            int eventId) => throw new NotSupportedException();

        public string? GetLatestSeason() => throw new NotSupportedException();
    }

    private sealed class TestSlashCommandInteraction : IDiscordSlashCommandInteraction
    {
        public string? Team { get; init; }

        public long? Gw { get; init; }

        public string Name => DiscordApplicationCommands.BenchLeagueName;

        public string UserMention => "<@123>";

        public string? GetStringOption(string name) =>
            string.Equals(name, "team", StringComparison.OrdinalIgnoreCase) ? Team : null;

        public long? GetIntegerOption(string name) =>
            string.Equals(name, "gw", StringComparison.OrdinalIgnoreCase) ? Gw : null;

        public List<string> Messages { get; } = [];

        public List<string> Operations { get; } = [];

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
