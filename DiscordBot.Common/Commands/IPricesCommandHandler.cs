namespace DiscordBot.Commands;

public interface IPricesCommandHandler
{
    Task HandleAsync(IDiscordSlashCommandInteraction interaction);
}
