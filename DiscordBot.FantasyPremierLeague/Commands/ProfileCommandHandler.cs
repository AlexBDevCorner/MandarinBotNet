using DiscordBot.FantasyPremierLeague.Recognition;
using DiscordBot.Notifications;

namespace DiscordBot.Commands;

public sealed class ProfileCommandHandler(
    FplRecognitionQueryService queryService,
    FplRecognitionMessageCompositionService messageComposer) : IProfileCommandHandler
{
    private const int DiscordMessageLimit = 2_000;

    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await interaction.DeferAsync();

        var managerQuery = interaction.GetStringOption("manager");
        if (string.IsNullOrWhiteSpace(managerQuery))
        {
            await interaction.ModifyOriginalResponseAsync(
                messageComposer.ComposeManagerNotFound(string.Empty));
            return;
        }

        var result = queryService.GetManagerProfile(managerQuery);
        var message = result.Outcome switch
        {
            FplManagerProfileLookupOutcome.Available =>
                messageComposer.ComposeProfile(result.Profile!),
            FplManagerProfileLookupOutcome.NoData =>
                messageComposer.ComposeNoData(),
            FplManagerProfileLookupOutcome.NotFound =>
                messageComposer.ComposeManagerNotFound(managerQuery),
            FplManagerProfileLookupOutcome.Ambiguous =>
                messageComposer.ComposeAmbiguousManager(
                    managerQuery,
                    result.Candidates!),
            _ => messageComposer.ComposeNoData()
        };

        await SendChunkedAsync(interaction, message);
    }

    private static async Task SendChunkedAsync(
        IDiscordSlashCommandInteraction interaction,
        string message)
    {
        var chunks = DiscordMessageChunker.Split(message, DiscordMessageLimit);
        await interaction.ModifyOriginalResponseAsync(chunks[0]);
        foreach (var chunk in chunks.Skip(1))
        {
            await interaction.FollowupAsync(chunk);
        }
    }
}
