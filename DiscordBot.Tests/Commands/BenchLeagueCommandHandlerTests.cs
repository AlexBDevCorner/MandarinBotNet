using AwesomeAssertions;
using DiscordBot.BenchWarming;
using DiscordBot.Commands;
using NUnit.Framework;

namespace DiscordBot.Tests.Commands;

[TestFixture]
public sealed class BenchLeagueCommandHandlerTests
{
    [Test]
    public async Task HandleAsync_WithStoredStandings_ShowsSeasonStandings()
    {
        // Arrange
        var store = new TestBenchWarmingLeagueStore
        {
            LatestSeason = "2026/27",
            Standings =
            [
                new BenchWarmingEntryStanding(100, "Team A", 42),
                new BenchWarmingEntryStanding(200, "Team B", 7)
            ]
        };
        var handler = new BenchLeagueCommandHandler(
            store,
            new BenchWarmingMessageComposer());
        var interaction = new TestSlashCommandInteraction();

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Operations.Should().Equal("Defer", "Modify");
        interaction.Messages.Should().Equal(
            "Лига Обогревателей Скамейки (сезон 2026/27):" +
            "\n:one: Team A — 42" +
            "\n:two: Team B — 7");
    }

    [Test]
    public async Task HandleAsync_NoCalculatedRounds_ShowsUnavailableMessage()
    {
        // Arrange
        var store = new TestBenchWarmingLeagueStore { LatestSeason = null };
        var handler = new BenchLeagueCommandHandler(
            store,
            new BenchWarmingMessageComposer());
        var interaction = new TestSlashCommandInteraction();

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().Equal(
            "The bench warming league is unavailable right now. Please try again later.");
    }

    private sealed class TestBenchWarmingLeagueStore : IBenchWarmingLeagueStore
    {
        public string? LatestSeason { get; init; }

        public IReadOnlyList<BenchWarmingEntryStanding> Standings { get; init; } = [];

        public bool IsRoundCalculated(string season, int eventId)
        {
            throw new NotSupportedException();
        }

        public void SaveRound(
            string season,
            int eventId,
            IReadOnlyList<BenchWarmingPlayerPoints> benchPoints,
            DateTimeOffset calculatedAtUtc)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<BenchWarmingEntryStanding> GetSeasonStandings(string season)
        {
            return Standings;
        }

        public string? GetLatestSeason()
        {
            return LatestSeason;
        }
    }

    private sealed class TestSlashCommandInteraction : IDiscordSlashCommandInteraction
    {
        public string Name => DiscordApplicationCommands.BenchLeagueName;

        public string UserMention => "<@123>";

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
