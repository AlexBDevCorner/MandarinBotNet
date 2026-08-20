namespace DiscordBot.Commands;

public interface IBenchLeagueCommandHandler
{
    Task HandleAsync(IDiscordSlashCommandInteraction interaction);
}
