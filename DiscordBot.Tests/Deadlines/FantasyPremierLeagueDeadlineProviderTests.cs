using AwesomeAssertions;
using DiscordBot.Deadlines;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.PremierLeague;
using DiscordBot.Responses;
using NUnit.Framework;

namespace DiscordBot.Tests.Deadlines;

[TestFixture]
public sealed class FantasyPremierLeagueDeadlineProviderTests
{
    [Test]
    public async Task GetNextAsync_FplHasNextEvent_ReturnsCompetitionDeadline()
    {
        // Arrange
        var expectedDeadline = new DateTimeOffset(
            2027,
            8,
            2,
            12,
            0,
            0,
            TimeSpan.Zero);
        var client = new TestFantasyPremierLeagueClient
        {
            Bootstrap = new BootstrapStaticResponse
            {
                Events =
                [
                    new PremierLeagueEvent { Id = 41 },
                    new PremierLeagueEvent
                    {
                        Id = 42,
                        IsNext = true,
                        DeadlineTimeEpoch = expectedDeadline.ToUnixTimeSeconds()
                    }
                ]
            }
        };
        var provider = new FantasyPremierLeagueDeadlineProvider(
            client,
            new DeadlineSelectionService());

        // Act
        var result = await provider.GetNextAsync(CancellationToken.None);

        // Assert
        result.Should().Be(new CompetitionDeadline(
            "FPL",
            "Gameweek",
            42,
            expectedDeadline));
    }

    private sealed class TestFantasyPremierLeagueClient
        : IFantasyPremierLeagueClient
    {
        public required BootstrapStaticResponse Bootstrap { get; init; }

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

        public Task<IReadOnlyList<PremierLeagueFixture>> GetFixturesAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
