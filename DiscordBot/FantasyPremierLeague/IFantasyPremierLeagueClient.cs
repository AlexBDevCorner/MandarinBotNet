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
}
