using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DiscordBot.FantasyPremierLeague.PriceChanges;

public sealed record FplPlayerPriceChange(
    int PlayerId,
    string PlayerName,
    int PreviousCost,
    int CurrentCost)
{
    public int Difference => checked(CurrentCost - PreviousCost);
}

public sealed record FplPriceChangeCheck(
    IReadOnlyDictionary<int, int> CurrentPrices,
    IReadOnlyList<FplPlayerPriceChange> Changes,
    long PreviousSnapshotVersion);

public static class FplPriceChangeSourceIdentifier
{
    public static string Create(
        long previousSnapshotVersion,
        IEnumerable<FplPlayerPriceChange> changes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(previousSnapshotVersion);
        ArgumentNullException.ThrowIfNull(changes);

        var orderedChanges = changes
            .OrderBy(change => change.PlayerId)
            .ThenBy(change => change.PreviousCost)
            .ThenBy(change => change.CurrentCost)
            .ToArray();
        if (orderedChanges.Length == 0)
        {
            throw new ArgumentException(
                "At least one price change is required.",
                nameof(changes));
        }

        var canonicalChanges = string.Join(
            '|',
            [
                previousSnapshotVersion.ToString(CultureInfo.InvariantCulture),
                .. orderedChanges.Select(change =>
                    string.Join(
                        ':',
                        change.PlayerId.ToString(CultureInfo.InvariantCulture),
                        change.PreviousCost.ToString(CultureInfo.InvariantCulture),
                        change.CurrentCost.ToString(CultureInfo.InvariantCulture)))
            ]);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalChanges));
        return $"prices-{Convert.ToHexString(hash)}";
    }
}
