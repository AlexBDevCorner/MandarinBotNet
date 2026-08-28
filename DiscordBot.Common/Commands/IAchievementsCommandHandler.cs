namespace DiscordBot.Commands;

public interface IAchievementsCommandHandler
{
    Task HandleAsync(IDiscordSlashCommandInteraction interaction);
}
