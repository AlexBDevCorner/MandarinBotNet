namespace DiscordBot.Commands;

public interface IHelpCommandHandler
{
    Task HandleAsync(IDiscordSlashCommandInteraction interaction);
}
