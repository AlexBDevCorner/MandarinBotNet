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

        summary.Append("\n\n🏆 Лайв-таблица:");
        foreach (var manager in gameweek.Managers)
        {
            AppendLiveManager(summary, manager);
        }

        summary.Append("\n\n🎯 Ещё в игре:");
        AppendPlayerProgress(summary, gameweek.Managers);

        summary.Append("\n\n⚔️ Что ещё может всё испортить:");
        AppendSwingInsights(summary, gameweek.SwingInsights);

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

        summary.Append("\n\n🛰️ Снимок турнирной таблицы FPL: ");
        summary.Append(FormatTimestamp(gameweek.SourceUpdatedAtUtc));
        summary.Append("; отчёт собран: ");
        summary.Append(FormatTimestamp(gameweek.CapturedAtUtc));

        return summary.ToString();
    }

    private static void AppendLiveManager(
        StringBuilder summary,
        FplLiveManagerInsights manager)
    {
        summary.Append('\n');
        summary.Append(FormatLiveRank(manager.LiveRank));
        summary.Append(' ');
        summary.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
        summary.Append(" (");
        summary.Append(DiscordTextSafety.SanitizeExternalName(manager.ManagerName));
        summary.Append(") — ");
        summary.Append(manager.LiveTotalPoints.ToString(CultureInfo.InvariantCulture));
        summary.Append(" очков (");
        summary.Append(FormatSigned(manager.LiveGameweekPoints));
        summary.Append(" за тур");
        if (manager.TransferCost > 0)
        {
            summary.Append(", -");
            summary.Append(manager.TransferCost.ToString(CultureInfo.InvariantCulture));
            summary.Append(" за трансферы");
        }

        summary.Append(')');
        if (manager.RankChange > 0)
        {
            summary.Append(" ↑");
            summary.Append(manager.RankChange.ToString(CultureInfo.InvariantCulture));
        }
        else if (manager.RankChange < 0)
        {
            summary.Append(" ↓");
            summary.Append((-manager.RankChange).ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            summary.Append(" —");
        }

        if (manager.GapToLeader > 0)
        {
            summary.Append(" — ");
            summary.Append(manager.GapToLeader.ToString(CultureInfo.InvariantCulture));
            summary.Append(" до лидера");
        }
    }

    private static void AppendPlayerProgress(
        StringBuilder summary,
        IEnumerable<FplLiveManagerInsights> managers)
    {
        foreach (var manager in managers)
        {
            summary.Append('\n');
            summary.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
            summary.Append(" — ");
            if (manager.PlayerProgress.Active == 0)
            {
                summary.Append("все закончили");
                continue;
            }

            var hasPreviousPart = false;
            if (manager.PlayerProgress.Playing > 0)
            {
                summary.Append(manager.PlayerProgress.Playing.ToString(
                    CultureInfo.InvariantCulture));
                summary.Append(" играет");
                hasPreviousPart = true;
            }

            if (manager.PlayerProgress.YetToPlay > 0)
            {
                if (hasPreviousPart)
                {
                    summary.Append(", ");
                }

                summary.Append(manager.PlayerProgress.YetToPlay.ToString(
                    CultureInfo.InvariantCulture));
                summary.Append(" ещё не начал");
            }
        }
    }

    private static void AppendSwingInsights(
        StringBuilder summary,
        IEnumerable<FplLiveSwingInsight> insights)
    {
        var swingInsights = insights.ToArray();
        if (swingInsights.Length == 0)
        {
            summary.Append("\n(пока нечему)");
            return;
        }

        foreach (var insight in swingInsights)
        {
            summary.Append("\n• ");
            switch (insight)
            {
                case CaptainClashInsight clash:
                    summary.Append(DiscordTextSafety.SanitizeExternalName(
                        clash.FirstEntryName));
                    summary.Append(": ");
                    summary.Append(DiscordTextSafety.SanitizeExternalName(
                        clash.FirstCaptain));
                    summary.Append(" (C) против ");
                    summary.Append(DiscordTextSafety.SanitizeExternalName(
                        clash.SecondCaptain));
                    summary.Append(" (C) у ");
                    summary.Append(DiscordTextSafety.SanitizeExternalName(
                        clash.SecondEntryName));
                    break;
                case UniqueRemainingPlayerInsight unique:
                    summary.Append(DiscordTextSafety.SanitizeExternalName(
                        unique.EntryName));
                    summary.Append(" — единственный с ");
                    summary.Append(DiscordTextSafety.SanitizeExternalName(
                        unique.PlayerName));
                    summary.Append(" среди активных составов");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(insight));
            }
        }
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

    private static string FormatLiveRank(int rank)
    {
        return rank switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => $"{rank.ToString(CultureInfo.InvariantCulture)}."
        };
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
