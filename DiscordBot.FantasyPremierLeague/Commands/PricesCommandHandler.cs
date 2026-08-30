using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.PriceChanges;
using DiscordBot.Notifications;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Commands;

public sealed class PricesCommandHandler(
    FplPriceChangeService priceChangeService,
    FplLeaguePriceChangeService leaguePriceChangeService,
    FplPriceChangeMessageCompositionService messageComposer,
    ILogger<PricesCommandHandler> logger) : IPricesCommandHandler
{
    private const int DiscordMessageLimit = 2_000;

    public async Task HandleAsync(IDiscordSlashCommandInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await interaction.DeferAsync();

        try
        {
            var check = await priceChangeService.CheckAsync(CancellationToken.None);
            var latestChanges = check.Changes.Count == 0
                ? priceChangeService.GetLatestChanges()
                : null;
            var changes = latestChanges?.Changes ?? check.Changes;
            if (changes.Count == 0)
            {
                await interaction.ModifyOriginalResponseAsync(
                    messageComposer.ComposeNoHistory());
                return;
            }

            var report = await leaguePriceChangeService.CreateReportAsync(
                changes,
                latestChanges?.CheckedAtUtc ?? check.CheckedAtUtc,
                latestChanges?.EventId ?? check.CurrentEventId,
                CancellationToken.None);
            var message = messageComposer.Compose(
                report,
                isLatestSavedBatch: latestChanges is not null);
            var chunks = DiscordMessageChunker.Split(message, DiscordMessageLimit);

            await interaction.ModifyOriginalResponseAsync(chunks[0]);
            foreach (var chunk in chunks.Skip(1))
            {
                await interaction.FollowupAsync(chunk);
            }
        }
        catch (FantasyPremierLeagueApiException exception)
        {
            logger.LogWarning(
                exception,
                "FPL prices command request failed with {FailureKind} and HTTP status {StatusCode}.",
                exception.FailureKind,
                exception.StatusCode);
            await interaction.ModifyOriginalResponseAsync(
                "⚠️ Изменения цен FPL сейчас недоступны. Попробуйте ещё раз позже.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "FPL prices command request failed with {FailureKind}.",
                "Unexpected");
            await interaction.ModifyOriginalResponseAsync(
                "⚠️ Изменения цен FPL сейчас недоступны. Попробуйте ещё раз позже.");
        }
    }
}
