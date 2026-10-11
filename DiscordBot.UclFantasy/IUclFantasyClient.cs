namespace DiscordBot.UclFantasy;

public interface IUclFantasyClient
{
    Task<UclFantasyWebConfigurationResponse> GetWebConfigurationAsync(
        CancellationToken cancellationToken);

    Task<UclFantasyFixturesResponse> GetFixturesAsync(
        UclFantasyWebConfigurationValue webConfiguration,
        CancellationToken cancellationToken);

    Task<UclLeagueStandings> GetLeagueStandingsAsync(
        UclLeagueStandingsQuery query,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("GetLeagueStandingsAsync is not implemented in this test double.");
}
