using DiscordBot.Responses;
using Microsoft.Extensions.Logging;

namespace DiscordBot.FantasyPremierLeague.PriceChanges;

public sealed class FplPriceChangeService(
    IFantasyPremierLeagueClient premierLeagueClient,
    IFplPriceSnapshotStore snapshotStore,
    ILogger<FplPriceChangeService> logger)
{
    public async Task<FplPriceChangeCheck> CheckAsync(
        CancellationToken cancellationToken)
    {
        var bootstrap = await premierLeagueClient.GetBootstrapStaticAsync(
            cancellationToken);
        var currentPrices = CreateCurrentPrices(bootstrap.Elements);
        FplPriceSnapshot? previousSnapshot;

        try
        {
            previousSnapshot = snapshotStore.GetSnapshot();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to read the previous FPL player price snapshot.");
            throw;
        }

        var previousPrices = previousSnapshot?.Prices;
        var changes = previousPrices is null || previousPrices.Count == 0
            ? []
            : bootstrap.Elements
                .Where(element =>
                    previousPrices.TryGetValue(element.Id, out var previousCost) &&
                    previousCost != element.NowCost)
                .Select(element => new FplPlayerPriceChange(
                    element.Id,
                    element.WebName,
                    previousPrices[element.Id],
                    element.NowCost))
                .OrderBy(change => change.PlayerName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(change => change.PlayerId)
                .ToArray();

        logger.LogInformation(
            "Checked FPL player prices for {PlayerCount} players and found " +
            "{PriceChangeCount} changes.",
            currentPrices.Count,
            changes.Length);
        return new FplPriceChangeCheck(
            currentPrices,
            changes,
            previousSnapshot?.Version ?? 0);
    }

    public void SaveSnapshot(FplPriceChangeCheck priceCheck)
    {
        ArgumentNullException.ThrowIfNull(priceCheck);

        try
        {
            snapshotStore.SaveSnapshot(priceCheck.CurrentPrices);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to persist the FPL player price snapshot for {PlayerCount} players.",
                priceCheck.CurrentPrices.Count);
            throw;
        }
    }

    private static IReadOnlyDictionary<int, int> CreateCurrentPrices(
        IReadOnlyList<PremierLeagueElement>? elements)
    {
        if (elements is null || elements.Count == 0)
        {
            throw new InvalidDataException(
                "The FPL bootstrap response did not include players.");
        }

        var currentPrices = new Dictionary<int, int>(elements.Count);
        foreach (var element in elements)
        {
            if (element.Id <= 0 ||
                element.NowCost <= 0 ||
                string.IsNullOrWhiteSpace(element.WebName))
            {
                throw new InvalidDataException(
                    "The FPL bootstrap response included incomplete player price data.");
            }

            if (!currentPrices.TryAdd(element.Id, element.NowCost))
            {
                throw new InvalidDataException(
                    $"The FPL bootstrap response included duplicate player {element.Id}.");
            }
        }

        return currentPrices;
    }
}
