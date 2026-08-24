namespace DiscordBot.Commands;

public interface IStandingsCommandHandler
{
    Task HandleAsync(IDiscordSlashCommandInteraction interaction);
}
