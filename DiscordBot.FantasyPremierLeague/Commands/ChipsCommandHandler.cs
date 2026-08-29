using DiscordBot.FantasyPremierLeague.Chips;
using DiscordBot.Notifications;

namespace DiscordBot.Commands;

public sealed class ChipsCommandHandler(
    FplChipCatalog chipCatalog,
    FplChipMessageCompositionService messageComposer)
    : IChipsCommandHandler
{
    private const int DiscordMessageLimit = 2_000;

    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await interaction.DeferAsync();

        var message = messageComposer.Compose(chipCatalog.GetAll());
        var chunks = DiscordMessageChunker.Split(message, DiscordMessageLimit);

        await interaction.ModifyOriginalResponseAsync(chunks[0]);

        foreach (var chunk in chunks.Skip(1))
        {
            await interaction.FollowupAsync(chunk);
        }
    }
}
