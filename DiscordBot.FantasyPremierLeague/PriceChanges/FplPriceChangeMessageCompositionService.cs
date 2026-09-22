using System.Globalization;
using System.Text;
using DiscordBot.Notifications;

namespace DiscordBot.FantasyPremierLeague.PriceChanges;

public sealed class FplPriceChangeMessageCompositionService
{
    private static readonly CultureInfo RussianCulture = new("ru-RU");

    public string Compose(
        FplLeaguePriceChangeReport report,
        bool isLatestSavedBatch = false)
    {
        ArgumentNullException.ThrowIfNull(report);

        var summary = new StringBuilder(
            isLatestSavedBatch
                ? "💰 Последние зафиксированные изменения цен FPL:"
                : "💰 Изменения цен FPL с последней проверки:");

        if (report.PlayerChanges.Count == 0)
        {
            summary.Append("\n🤷 Изменений нет.");
            return summary.ToString();
        }

        if (!report.LeagueDataAvailable)
        {
            foreach (var playerChange in report.PlayerChanges)
            {
                AppendPlayerChange(summary, playerChange.Change, []);
            }

            summary.Append(
                "\n\n⚠️ Не удалось сопоставить игроков с составами нашей лиги.");
            return summary.ToString();
        }

        var hasAffectedSquads = report.PlayerChanges
            .Any(change => change.OwnerEntryNames.Count > 0);
        if (!hasAffectedSquads)
        {
            summary.Append(
                "\n😌 Цены изменились, но составы нашей лиги это не затронуло.");

            foreach (var playerChange in report.PlayerChanges)
            {
                AppendPlayerChange(
                    summary,
                    playerChange.Change,
                    playerChange.OwnerEntryNames);
            }
        }
        else
        {
            var leagueDifference = report.TeamImpacts.Sum(impact => impact.Difference);
            summary.Append("\n💸 Общая стоимость составов: ");
            summary.Append(FormatSignedPrice(leagueDifference));

            foreach (var playerChange in report.PlayerChanges)
            {
                AppendPlayerChange(
                    summary,
                    playerChange.Change,
                    playerChange.OwnerEntryNames);
            }

            summary.Append("\n\n📋 По командам:");
            foreach (var impact in report.TeamImpacts)
            {
                summary.Append("\n• ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(impact.EntryName));
                summary.Append(": ");
                summary.Append(FormatSignedPrice(impact.Difference));
            }
        }

        if (report.AvailableSquadCount < report.LeagueManagerCount)
        {
            summary.Append("\n\n⚠️ Составы загружены для ");
            summary.Append(report.AvailableSquadCount.ToString(CultureInfo.InvariantCulture));
            summary.Append(" из ");
            summary.Append(report.LeagueManagerCount.ToString(CultureInfo.InvariantCulture));
            summary.Append(" менеджеров.");
        }

        return summary.ToString();
    }

    public string ComposeNoHistory()
    {
        return "💰 Истории изменений цен пока нет. " +
            "Она появится после первой плановой проверки цен.";
    }

    private static void AppendPlayerChange(
        StringBuilder summary,
        FplPlayerPriceChange change,
        IReadOnlyList<string> owners)
    {
        var isIncrease = change.Difference > 0;
        summary.Append('\n');
        summary.Append(isIncrease ? "📈 " : "📉 ");
        summary.Append(DiscordTextSafety.SanitizeExternalName(change.PlayerName));
        summary.Append(": ");
        summary.Append(FormatPrice(change.PreviousCost));
        summary.Append(" → ");
        summary.Append(FormatPrice(change.CurrentCost));
        summary.Append(" (");
        summary.Append(FormatSignedPrice(change.Difference));
        summary.Append(')');

        if (owners.Count == 0)
        {
            return;
        }

        summary.Append("\n  В составах: ");
        summary.Append(string.Join(
            ", ",
            owners.Select(DiscordTextSafety.SanitizeExternalName)));
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
