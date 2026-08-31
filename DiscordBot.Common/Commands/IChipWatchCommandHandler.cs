namespace DiscordBot.Commands;

public interface IChipWatchCommandHandler
{
    Task HandleAsync(IDiscordSlashCommandInteraction interaction);
}
