using System.Globalization;
using System.Text;
using DiscordBot.Notifications;
using DiscordBot.PremierLeague;

namespace DiscordBot.UclFantasy;

public sealed class UclFantasyMessageCompositionService(
    ConfiguredTimeZone configuredTimeZone)
{
    public const int StandingsPageSize = 10;

    private const string StandingsHeading = "🏆 Таблица лиги UCL:";
    private const string StandingsUpdateHeading = "🔔 Обновление таблицы UCL:";
    private const string EmptyStandingsMessage =
        "🤷 Данные о позициях не получены.";
    private const string UnavailableStandingsMessage =
        "⚠️ Таблица UCL сейчас недоступна. Попробуйте ещё раз позже.";

    public string ComposeDeadlineReminder(
        DateTimeOffset deadlineUtc,
        DateTimeOffset utcNow)
    {
        _ = configuredTimeZone;
        _ = utcNow;
        var absoluteTimestamp = DiscordTimestamp.FormatAbsolute(deadlineUtc);
        var relativeTimestamp = DiscordTimestamp.FormatRelative(deadlineUtc);

        return $"⚽ Дедлайн игрового дня ЛЧ!" +
            $"\nДедлайн: {absoluteTimestamp}" +
            $"\nОсталось: {relativeTimestamp} ⏳";
    }

    public string ComposeStandingsPage(
        UclLeagueStandings standings,
        int page)
    {
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);

        var totalPages = GetTotalPages(standings.Members.Count, StandingsPageSize);
        if (standings.Members.Count == 0)
        {
            return $"{ComposeStandingsHeader(standings)}\n{EmptyStandingsMessage}";
        }

        if (page > totalPages)
        {
            return $"{ComposeStandingsHeader(standings)}\n⚠️ Страница {page.ToString(CultureInfo.InvariantCulture)} недоступна. Всего страниц: {totalPages.ToString(CultureInfo.InvariantCulture)}.";
        }

        var rows = standings.Members
            .OrderBy(member => member.Rank <= 0 ? int.MaxValue : member.Rank)
            .ThenBy(member => member.StableId)
            .Skip((page - 1) * StandingsPageSize)
            .Take(StandingsPageSize)
            .ToList();

        var summary = new StringBuilder(ComposeStandingsHeader(standings));
        AppendRows(summary, rows);
        summary.Append(CultureInfo.InvariantCulture, $"\n\nСтраница {page} из {totalPages} • Всего {standings.Members.Count}");
        return summary.ToString();
    }

    public string ComposeStandingsUpdate(UclLeagueStandings standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        if (standings.Members.Count == 0)
        {
            return $"{ComposeStandingsHeader(standings, update: true)}\n{EmptyStandingsMessage}";
        }

        var ordered = standings.Members
            .OrderBy(member => member.Rank <= 0 ? int.MaxValue : member.Rank)
            .ThenBy(member => member.StableId)
            .ToList();

        var summary = new StringBuilder(ComposeStandingsHeader(standings, update: true));
        AppendRows(summary, ordered);

        var movers = ordered.Where(member => member.HasMovement).ToList();
        if (movers.Count > 0)
        {
            summary.Append("\n\n📈 Движения:");
            foreach (var mover in movers.Take(10))
            {
                var arrow = mover.MovedUp ? "↑" : "↓";
                summary.Append(CultureInfo.InvariantCulture, $"\n{DiscordTextSafety.SanitizeExternalName(mover.TeamName)} {arrow}{mover.MovementDelta}");
            }

            if (movers.Count > 10)
            {
                summary.Append(CultureInfo.InvariantCulture, $"\n…и ещё {movers.Count - 10}");
            }
        }

        return summary.ToString();
    }

    public static string ComposeUnavailableStandings()
    {
        return $"{StandingsHeading}\n{UnavailableStandingsMessage}";
    }

    private static string ComposeStandingsHeader(
        UclLeagueStandings standings,
        bool update = false)
    {
        var heading = update ? StandingsUpdateHeading : StandingsHeading;
        var league = DiscordTextSafety.SanitizeExternalName(standings.LeagueName);
        return string.Concat(
            heading,
            "\n",
            league,
            " (ID ",
            standings.LeagueId.ToString(CultureInfo.InvariantCulture),
            ") • Matchday ",
            standings.MatchdayId.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendRows(StringBuilder summary, IReadOnlyList<UclLeagueStanding> rows)
    {
        if (rows.Count == 0)
        {
            summary.Append('\n');
            summary.Append(EmptyStandingsMessage);
            return;
        }

        var leaderTotal = rows.Count > 0 ? rows.Max(member => member.TotalPoints) : 0;
        foreach (var row in rows)
        {
            summary.Append('\n');
            summary.Append(GetRankLabel(row.Rank));
            summary.Append(' ');
            summary.Append(DiscordTextSafety.SanitizeExternalName(row.TeamName));
            if (!string.IsNullOrWhiteSpace(row.ManagerName))
            {
                summary.Append(" (");
                summary.Append(DiscordTextSafety.SanitizeExternalName(row.ManagerName));
                summary.Append(')');
            }

            summary.Append(" — ");
            summary.Append(FormatTotal(row.TotalPoints));
            summary.Append(" | MD ");
            summary.Append(row.MatchdayPoints.ToString(CultureInfo.InvariantCulture));

            if (row.HasMovement)
            {
                var arrow = row.MovedUp ? '↑' : '↓';
                summary.Append(' ');
                summary.Append(arrow);
                summary.Append(row.MovementDelta.ToString(CultureInfo.InvariantCulture));
            }

            if (row.Rank != 1 && leaderTotal - row.TotalPoints > 0)
            {
                summary.Append(" | ");
                summary.Append((leaderTotal - row.TotalPoints).ToString(CultureInfo.InvariantCulture));
                summary.Append(" до лидера");
            }
        }
    }

    private static int GetTotalPages(int totalMembers, int pageSize)
    {
        return Math.Max(1, (totalMembers + pageSize - 1) / pageSize);
    }

    private static string GetRankLabel(int rank) => rank switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => $"{rank.ToString(CultureInfo.InvariantCulture)}."
    };

    private static string FormatTotal(int total)
    {
        return total.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', ' ');
    }
}
