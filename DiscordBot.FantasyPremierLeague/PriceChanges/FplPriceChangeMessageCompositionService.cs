using System.Globalization;
using System.Text;
using DiscordBot.Notifications;

namespace DiscordBot.FantasyPremierLeague.PriceChanges;

public sealed class FplPriceChangeMessageCompositionService
{
    private static readonly CultureInfo RussianCulture = new("ru-RU");

    public string Compose(IEnumerable<FplPlayerPriceChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var priceChanges = changes.ToArray();
        var summary = new StringBuilder("💰 Изменения цен игроков FPL:");
        if (priceChanges.Length == 0)
        {
            summary.Append("\n🤷 Изменений нет.");
            return summary.ToString();
        }

        foreach (var change in priceChanges)
        {
            var isIncrease = change.Difference > 0;
            var direction = isIncrease ? "📈" : "📉";
            var verb = isIncrease ? "подорожал" : "подешевел";
            summary.Append('\n');
            summary.Append(direction);
            summary.Append(' ');
            summary.Append(DiscordTextSafety.SanitizeExternalName(change.PlayerName));
            summary.Append(' ');
            summary.Append(verb);
            summary.Append(": ");
            summary.Append(FormatPrice(change.PreviousCost));
            summary.Append(" → ");
            summary.Append(FormatPrice(change.CurrentCost));
            summary.Append(" (");
            summary.Append(FormatSignedPrice(change.Difference));
            summary.Append(')');
        }

        return summary.ToString();
    }

    private static string FormatPrice(int cost)
    {
        return $"{(cost / 10m).ToString("0.0", RussianCulture)} млн £";
    }

    private static string FormatSignedPrice(int difference)
    {
        var priceDifference = difference / 10m;
        var sign = priceDifference > 0 ? "+" : string.Empty;
        return $"{sign}{priceDifference.ToString("0.0", RussianCulture)} млн £";
    }
}
