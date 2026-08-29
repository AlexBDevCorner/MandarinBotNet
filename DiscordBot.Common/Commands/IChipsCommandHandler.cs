namespace DiscordBot.Commands;

public interface IChipsCommandHandler
{
    Task HandleAsync(IDiscordSlashCommandInteraction interaction);
}
