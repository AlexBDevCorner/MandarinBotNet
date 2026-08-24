namespace DiscordBot.Commands;

public interface IDeadlineCommandHandler
{
    Task HandleAsync(IDiscordSlashCommandInteraction interaction);
}
