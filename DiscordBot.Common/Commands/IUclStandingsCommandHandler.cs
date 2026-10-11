namespace DiscordBot.Commands;

public interface IUclStandingsCommandHandler
{
    Task HandleAsync(IDiscordSlashCommandInteraction interaction);
}
