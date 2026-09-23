namespace DiscordBot.Commands;

public interface IEventWatchCommandHandler
{
    Task HandleAsync(IDiscordSlashCommandInteraction interaction);
}
