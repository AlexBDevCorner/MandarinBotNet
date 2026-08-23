using System.Globalization;
using System.Text;
using DiscordBot.Notifications;

namespace DiscordBot.FantasyPremierLeague.Live;

public sealed class FplLiveInsightsMessageComposer(
    FantasyPremierLeagueOptions options)
{
    public string Compose(FplLiveInsightsResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Availability switch
        {
            FplLiveInsightsAvailability.Available =>
                ComposeAvailable(result.Gameweek ?? throw new InvalidDataException(
                    "An available FPL live insights result did not include a gameweek.")),
            FplLiveInsightsAvailability.NoActiveGameweek =>
                "⏸️ Сейчас нет активного тура FPL, поэтому лайв-отчёт недоступен.",
            FplLiveInsightsAvailability.Stale => ComposeStale(
                result.Gameweek ?? throw new InvalidDataException(
                    "A stale FPL live insights result did not include a gameweek.")),
            FplLiveInsightsAvailability.Unavailable =>
                result.FailureKind == FantasyPremierLeagueFailureKind.Transient
                    ? "⚠️ Лайв-отчёт FPL временно недоступен: источник данных не отвечает " +
                      "или ограничил запросы. Попробуйте ещё раз позже."
                    : "⚠️ Лайв-отчёт FPL сейчас недоступен. Попробуйте ещё раз позже.",
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private string ComposeAvailable(FplLiveGameweek gameweek)
    {
        var summary = new StringBuilder(
            $"⚡ FPL в прямом эфире — тур {gameweek.EventId.ToString(CultureInfo.InvariantCulture)} " +
            $"(сезон {DiscordTextSafety.SanitizeExternalName(gameweek.Season)}):");
        summary.Append("\n🛰️ Данные источника обновлены: ");
        summary.Append(FormatTimestamp(gameweek.SourceUpdatedAtUtc));
        summary.Append("; отчёт собран: ");
        summary.Append(FormatTimestamp(gameweek.CapturedAtUtc));

        summary.Append("\n\n👥 Менеджеры:");
        foreach (var manager in gameweek.Managers)
        {
            summary.Append('\n');
            summary.Append(manager.Rank.ToString(CultureInfo.InvariantCulture));
            summary.Append(". ");
            summary.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
            summary.Append(" (");
            summary.Append(DiscordTextSafety.SanitizeExternalName(manager.ManagerName));
            summary.Append(") — ");
            summary.Append(manager.LivePoints.ToString(CultureInfo.InvariantCulture));
            summary.Append(" очков в лайве; игроков осталось: ");
            summary.Append(manager.PlayersRemainingToPlay.ToString(CultureInfo.InvariantCulture));
        }

        summary.Append("\n\n🪑 Очки на скамейке (от ");
        summary.Append(options.LargeBenchPointsThreshold.ToString(CultureInfo.InvariantCulture));
        summary.Append("):");
        AppendBenchAlerts(summary, gameweek.BenchAlerts);

        summary.Append("\n\n🛟 Спасение автозаменой:");
        if (gameweek.AutomaticSubstitutionSalvations.Count == 0)
        {
            summary.Append("\n(пока нет)");
        }
        else
        {
            foreach (var salvation in gameweek.AutomaticSubstitutionSalvations)
            {
                summary.Append('\n');
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    salvation.EntryName));
                summary.Append(" — ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    salvation.PlayerInName));
                summary.Append(" (");
                summary.Append(salvation.PlayerInPoints.ToString(CultureInfo.InvariantCulture));
                summary.Append(") заменил ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    salvation.PlayerOutName));
                summary.Append(" (");
                summary.Append(salvation.PlayerOutPoints.ToString(CultureInfo.InvariantCulture));
                summary.Append("); ");
                summary.Append(FormatSigned(salvation.SavedPoints));
                summary.Append(" очков спасено");
            }
        }

        summary.Append("\n\n💥 Капитанские провалы (капитан ≤ ");
        summary.Append(options.CaptainDisasterPointsThreshold.ToString(
            CultureInfo.InvariantCulture));
        summary.Append(", вице-капитан ≥ ");
        summary.Append(options.CaptainDisasterViceCaptainPointsThreshold.ToString(
            CultureInfo.InvariantCulture));
        summary.Append("):");
        AppendCaptainDisasters(summary, gameweek.CaptainDisasters);

        summary.Append("\n\n🧠 Удачный выбор капитана (с учётом множителя от ");
        summary.Append(options.CaptainSuccessEffectivePointsThreshold.ToString(
            CultureInfo.InvariantCulture));
        summary.Append(" очков):");
        if (gameweek.CaptainSuccesses.Count == 0)
        {
            summary.Append("\n(пока нет)");
        }
        else
        {
            foreach (var manager in gameweek.CaptainSuccesses)
            {
                summary.Append('\n');
                summary.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
                summary.Append(" — ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    manager.Captain.CaptainName));
                summary.Append(" ");
                summary.Append(manager.Captain.CaptainEffectivePoints.ToString(
                    CultureInfo.InvariantCulture));
                summary.Append(" очков с учётом множителя");
            }
        }

        return summary.ToString();
    }

    private string ComposeStale(FplLiveGameweek gameweek)
    {
        return $"⌛ Лайв-данные FPL для тура {gameweek.EventId.ToString(CultureInfo.InvariantCulture)} " +
            $"(сезон {DiscordTextSafety.SanitizeExternalName(gameweek.Season)}) устарели. " +
            $"Данные источника обновлены: {FormatTimestamp(gameweek.SourceUpdatedAtUtc)}; " +
            $"отчёт собран: {FormatTimestamp(gameweek.CapturedAtUtc)}; " +
            $"допустимый возраст: {options.LiveDataMaxAge.TotalMinutes.ToString("0.#", CultureInfo.InvariantCulture)} " +
            "мин. Попробуйте ещё раз позже.";
    }

    private static void AppendBenchAlerts(
        StringBuilder summary,
        IEnumerable<FplLiveManagerInsights> managers)
    {
        var alerts = managers.ToArray();
        if (alerts.Length == 0)
        {
            summary.Append("\n(пока нет)");
            return;
        }

        foreach (var manager in alerts)
        {
            summary.Append('\n');
            summary.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
            summary.Append(" — ");
            summary.Append(manager.BenchPoints.ToString(CultureInfo.InvariantCulture));
            summary.Append(" очков на скамейке");
        }
    }

    private static void AppendCaptainDisasters(
        StringBuilder summary,
        IEnumerable<FplLiveManagerInsights> managers)
    {
        var disasters = managers.ToArray();
        if (disasters.Length == 0)
        {
            summary.Append("\n(пока нет)");
            return;
        }

        foreach (var manager in disasters)
        {
            summary.Append('\n');
            summary.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
            summary.Append(" — капитан ");
            summary.Append(DiscordTextSafety.SanitizeExternalName(
                manager.Captain.CaptainName));
            summary.Append(" ");
            summary.Append(manager.Captain.CaptainPoints.ToString(CultureInfo.InvariantCulture));
            summary.Append(" против вице-капитана ");
            summary.Append(DiscordTextSafety.SanitizeExternalName(
                manager.Captain.ViceCaptainName));
            summary.Append(" ");
            summary.Append(manager.Captain.ViceCaptainPoints.ToString(
                CultureInfo.InvariantCulture));
        }
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToUniversalTime().ToString(
            "yyyy-MM-dd HH:mm:ss 'UTC'",
            CultureInfo.InvariantCulture);
    }

    private static string FormatSigned(int value)
    {
        return value > 0
            ? $"+{value.ToString(CultureInfo.InvariantCulture)}"
            : value.ToString(CultureInfo.InvariantCulture);
    }
}
