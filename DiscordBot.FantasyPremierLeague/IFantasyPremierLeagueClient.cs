using DiscordBot.Responses;

namespace DiscordBot.FantasyPremierLeague;

public interface IFantasyPremierLeagueClient
{
    Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
        CancellationToken cancellationToken);

    Task<ClassicStandingsResponse> GetClassicStandingsAsync(
        int leagueId,
        CancellationToken cancellationToken);

    Task<HeadToHeadStandingsResponse> GetHeadToHeadStandingsAsync(
        int leagueId,
        CancellationToken cancellationToken);

    Task<EntryEventPicksResponse> GetEntryEventPicksAsync(
        int entryId,
        int eventId,
        CancellationToken cancellationToken);

    Task<EventLiveResponse> GetEventLiveAsync(
        int eventId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PremierLeagueFixture>> GetFixturesAsync(
        int eventId,
        CancellationToken cancellationToken);

    Task<EntryHistoryResponse> GetEntryHistoryAsync(
        int entryId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("GetEntryHistoryAsync is not implemented in this test double.");
}
