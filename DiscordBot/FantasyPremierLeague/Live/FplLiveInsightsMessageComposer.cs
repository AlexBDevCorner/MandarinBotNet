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
                "FPL live insights are unavailable because there is no active gameweek.",
            FplLiveInsightsAvailability.Stale => ComposeStale(
                result.Gameweek ?? throw new InvalidDataException(
                    "A stale FPL live insights result did not include a gameweek.")),
            FplLiveInsightsAvailability.Unavailable =>
                result.FailureKind == FantasyPremierLeagueFailureKind.Transient
                    ? "FPL live insights are temporarily unavailable due to an upstream " +
                      "failure or rate limit. Please try again later."
                    : "FPL live insights are unavailable right now. Please try again later.",
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private string ComposeAvailable(FplLiveGameweek gameweek)
    {
        var summary = new StringBuilder(
            $"FPL live insights - Gameweek {gameweek.EventId.ToString(CultureInfo.InvariantCulture)} " +
            $"(season {DiscordTextSafety.SanitizeExternalName(gameweek.Season)}):");
        summary.Append("\nSource updated: ");
        summary.Append(FormatTimestamp(gameweek.SourceUpdatedAtUtc));
        summary.Append("; captured: ");
        summary.Append(FormatTimestamp(gameweek.CapturedAtUtc));

        summary.Append("\n\nManagers:");
        foreach (var manager in gameweek.Managers)
        {
            summary.Append('\n');
            summary.Append(manager.Rank.ToString(CultureInfo.InvariantCulture));
            summary.Append(". ");
            summary.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
            summary.Append(" (");
            summary.Append(DiscordTextSafety.SanitizeExternalName(manager.ManagerName));
            summary.Append(") - ");
            summary.Append(manager.LivePoints.ToString(CultureInfo.InvariantCulture));
            summary.Append(" live points; ");
            summary.Append(manager.PlayersRemainingToPlay.ToString(CultureInfo.InvariantCulture));
            summary.Append(" players remaining");
        }

        summary.Append("\n\nBench alerts (>= ");
        summary.Append(options.LargeBenchPointsThreshold.ToString(CultureInfo.InvariantCulture));
        summary.Append(" points):");
        AppendBenchAlerts(summary, gameweek.BenchAlerts);

        summary.Append("\n\nAutomatic-substitution salvation:");
        if (gameweek.AutomaticSubstitutionSalvations.Count == 0)
        {
            summary.Append("\n(none)");
        }
        else
        {
            foreach (var salvation in gameweek.AutomaticSubstitutionSalvations)
            {
                summary.Append('\n');
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    salvation.EntryName));
                summary.Append(" - ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    salvation.PlayerInName));
                summary.Append(" (");
                summary.Append(salvation.PlayerInPoints.ToString(CultureInfo.InvariantCulture));
                summary.Append(") replaced ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    salvation.PlayerOutName));
                summary.Append(" (");
                summary.Append(salvation.PlayerOutPoints.ToString(CultureInfo.InvariantCulture));
                summary.Append("); ");
                summary.Append(FormatSigned(salvation.SavedPoints));
                summary.Append(" points saved");
            }
        }

        summary.Append("\n\nCaptain disasters (captain <= ");
        summary.Append(options.CaptainDisasterPointsThreshold.ToString(
            CultureInfo.InvariantCulture));
        summary.Append(", vice-captain >= ");
        summary.Append(options.CaptainDisasterViceCaptainPointsThreshold.ToString(
            CultureInfo.InvariantCulture));
        summary.Append("):");
        AppendCaptainDisasters(summary, gameweek.CaptainDisasters);

        summary.Append("\n\nCaptain successes (effective points >= ");
        summary.Append(options.CaptainSuccessEffectivePointsThreshold.ToString(
            CultureInfo.InvariantCulture));
        summary.Append("):");
        if (gameweek.CaptainSuccesses.Count == 0)
        {
            summary.Append("\n(none)");
        }
        else
        {
            foreach (var manager in gameweek.CaptainSuccesses)
            {
                summary.Append('\n');
                summary.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
                summary.Append(" - ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    manager.Captain.CaptainName));
                summary.Append(" ");
                summary.Append(manager.Captain.CaptainEffectivePoints.ToString(
                    CultureInfo.InvariantCulture));
                summary.Append(" effective points");
            }
        }

        return summary.ToString();
    }

    private string ComposeStale(FplLiveGameweek gameweek)
    {
        return $"FPL live insights for Gameweek {gameweek.EventId.ToString(CultureInfo.InvariantCulture)} " +
            $"(season {DiscordTextSafety.SanitizeExternalName(gameweek.Season)}) are stale. " +
            $"Source updated: {FormatTimestamp(gameweek.SourceUpdatedAtUtc)}; " +
            $"captured: {FormatTimestamp(gameweek.CapturedAtUtc)}; " +
            $"maximum age: {options.LiveDataMaxAge.TotalMinutes.ToString("0.#", CultureInfo.InvariantCulture)} " +
            "minutes. Please try again later.";
    }

    private static void AppendBenchAlerts(
        StringBuilder summary,
        IEnumerable<FplLiveManagerInsights> managers)
    {
        var alerts = managers.ToArray();
        if (alerts.Length == 0)
        {
            summary.Append("\n(none)");
            return;
        }

        foreach (var manager in alerts)
        {
            summary.Append('\n');
            summary.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
            summary.Append(" - ");
            summary.Append(manager.BenchPoints.ToString(CultureInfo.InvariantCulture));
            summary.Append(" bench points");
        }
    }

    private static void AppendCaptainDisasters(
        StringBuilder summary,
        IEnumerable<FplLiveManagerInsights> managers)
    {
        var disasters = managers.ToArray();
        if (disasters.Length == 0)
        {
            summary.Append("\n(none)");
            return;
        }

        foreach (var manager in disasters)
        {
            summary.Append('\n');
            summary.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
            summary.Append(" - captain ");
            summary.Append(DiscordTextSafety.SanitizeExternalName(
                manager.Captain.CaptainName));
            summary.Append(" ");
            summary.Append(manager.Captain.CaptainPoints.ToString(CultureInfo.InvariantCulture));
            summary.Append(" vs vice-captain ");
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
