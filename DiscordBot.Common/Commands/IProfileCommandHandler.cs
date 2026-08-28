namespace DiscordBot.Commands;

public interface IProfileCommandHandler
{
    Task HandleAsync(IDiscordSlashCommandInteraction interaction);
}
