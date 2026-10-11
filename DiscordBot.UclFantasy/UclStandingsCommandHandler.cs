using DiscordBot.Commands;
using DiscordBot.Notifications;
using Microsoft.Extensions.Logging;

namespace DiscordBot.UclFantasy;

public sealed class UclStandingsCommandHandler(
    UclLeagueStandingsProvider standingsProvider,
    UclFantasyMessageCompositionService messageComposer,
    ILogger<UclStandingsCommandHandler> logger) : IUclStandingsCommandHandler
{
    private const int DiscordMessageLimit = 2_000;

    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        var page = interaction.GetIntegerOption("page");
        if (page is < 1)
        {
            await interaction.RespondAsync(
                "⚠️ Страница должна быть не меньше 1.");
            return;
        }

        await interaction.DeferAsync();

        UclLeagueStandings standings;
        try
        {
            standings = await standingsProvider.GetCurrentStandingsAsync(CancellationToken.None);
        }
        catch (UclFantasyApiException exception)
        {
            logger.LogWarning(
                exception,
                "UCL standings command request failed with {FailureKind} and HTTP status {StatusCode}.",
                exception.FailureKind,
                exception.StatusCode);
            await interaction.ModifyOriginalResponseAsync(
                UclFantasyMessageCompositionService.ComposeUnavailableStandings());
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "UCL standings command request failed with {FailureKind}.",
                "Unexpected");
            await interaction.ModifyOriginalResponseAsync(
                UclFantasyMessageCompositionService.ComposeUnavailableStandings());
            return;
        }

        var requestedPage = page is null ? 1 : (int)page.Value;
        var message = messageComposer.ComposeStandingsPage(standings, requestedPage);
        var chunks = DiscordMessageChunker.Split(message, DiscordMessageLimit);

        await interaction.ModifyOriginalResponseAsync(chunks[0]);
        foreach (var chunk in chunks.Skip(1))
        {
            await interaction.FollowupAsync(chunk);
        }
    }
}
