namespace DiscordBot.UclFantasy;

public interface IUclFantasyClient
{
    Task<UclFantasyWebConfigurationResponse> GetWebConfigurationAsync(
        CancellationToken cancellationToken);

    Task<UclFantasyFixturesResponse> GetFixturesAsync(
        UclFantasyWebConfigurationValue webConfiguration,
        CancellationToken cancellationToken);
}
