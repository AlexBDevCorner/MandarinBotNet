namespace DiscordBot.Commands;

public interface ILiveInsightsCommandHandler
{
    Task HandleAsync(IDiscordSlashCommandInteraction interaction);
}
