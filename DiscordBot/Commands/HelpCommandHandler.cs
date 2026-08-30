namespace DiscordBot.Commands;

public sealed class HelpCommandHandler(
    BotHelpMessageCompositionService messageComposer) : IHelpCommandHandler
{
    public Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        return interaction.RespondAsync(messageComposer.ComposeGuide());
    }
}
