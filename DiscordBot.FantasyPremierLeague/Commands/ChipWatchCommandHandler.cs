using DiscordBot.Commands;
using DiscordBot.FantasyPremierLeague.ChipWatch;
using DiscordBot.Notifications;

namespace DiscordBot.FantasyPremierLeague.Commands;

public sealed class ChipWatchCommandHandler(
    FplChipWatchService chipWatchService,
    FplChipWatchSelectionService selectionService,
    FplChipWatchMessageCompositionService messageComposer) : IChipWatchCommandHandler
{
    private const int DiscordMessageLimit = 2_000;

    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await interaction.DeferAsync();

        var teamQuery = interaction.GetStringOption("team");

        FplChipWatchReport? report;
        try
        {
            report = await chipWatchService.CreateReportAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // Let global handling? For now return friendly message
            var errorMessage = "⚠️ Не удалось загрузить данные Chip Watch. Попробуйте ещё раз позже.";
            var chunks = DiscordMessageChunker.Split(errorMessage, DiscordMessageLimit);
            await interaction.ModifyOriginalResponseAsync(chunks[0]);
            foreach (var chunk in chunks.Skip(1))
            {
                await interaction.FollowupAsync(chunk);
            }

            return;
        }

        if (report is null)
        {
            var noEventMessage = messageComposer.ComposeNoUpcomingEvent();
            var chunks = DiscordMessageChunker.Split(noEventMessage, DiscordMessageLimit);
            await interaction.ModifyOriginalResponseAsync(chunks[0]);
            foreach (var chunk in chunks.Skip(1))
            {
                await interaction.FollowupAsync(chunk);
            }

            return;
        }

        string message;
        if (string.IsNullOrWhiteSpace(teamQuery))
        {
            message = messageComposer.ComposeLeagueOverview(report);
        }
        else
        {
            var selection = selectionService.Select(report.Managers, teamQuery);
            message = selection.Status switch
            {
                FplChipWatchSelectionStatus.Found => messageComposer.ComposeManagerDetails(
                    selection.Manager!,
                    report.TargetEventId,
                    report.DeadlineUtc,
                    report.SourceSquadEventId,
                    report.FinalEventId),
                FplChipWatchSelectionStatus.NotFound => messageComposer.ComposeNotFound(teamQuery),
                FplChipWatchSelectionStatus.Ambiguous => messageComposer.ComposeAmbiguous(teamQuery, selection.Candidates),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        var messageChunks = DiscordMessageChunker.Split(message, DiscordMessageLimit);
        await interaction.ModifyOriginalResponseAsync(messageChunks[0]);
        foreach (var chunk in messageChunks.Skip(1))
        {
            await interaction.FollowupAsync(chunk);
        }
    }
}
