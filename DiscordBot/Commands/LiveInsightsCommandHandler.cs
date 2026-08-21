using DiscordBot.FantasyPremierLeague.Live;
using DiscordBot.Notifications;

namespace DiscordBot.Commands;

public sealed class LiveInsightsCommandHandler(
    FplLiveInsightsService liveInsightsService,
    FplLiveInsightsMessageComposer messageComposer) : ILiveInsightsCommandHandler
{
    private const int DiscordMessageLimit = 2_000;

    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await interaction.DeferAsync();

        var result = await liveInsightsService.GetCurrentAsync(CancellationToken.None);
        var chunks = DiscordMessageChunker.Split(
            messageComposer.Compose(result),
            DiscordMessageLimit);

        await interaction.ModifyOriginalResponseAsync(chunks[0]);
        foreach (var chunk in chunks.Skip(1))
        {
            await interaction.FollowupAsync(chunk);
        }
    }
}
