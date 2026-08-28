using DiscordBot.FantasyPremierLeague.Recognition;
using DiscordBot.Notifications;

namespace DiscordBot.Commands;

public sealed class AchievementsCommandHandler(
    FplRecognitionQueryService queryService,
    FplRecognitionMessageCompositionService messageComposer)
    : IAchievementsCommandHandler
{
    private const int DiscordMessageLimit = 2_000;

    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await interaction.DeferAsync();

        var summary = queryService.GetSeasonSummary();
        if (summary is null)
        {
            await interaction.ModifyOriginalResponseAsync(
                messageComposer.ComposeNoData());
            return;
        }

        var message = messageComposer.ComposeSeasonSummary(summary);
        var chunks = DiscordMessageChunker.Split(message, DiscordMessageLimit);
        await interaction.ModifyOriginalResponseAsync(chunks[0]);
        foreach (var chunk in chunks.Skip(1))
        {
            await interaction.FollowupAsync(chunk);
        }
    }
}
