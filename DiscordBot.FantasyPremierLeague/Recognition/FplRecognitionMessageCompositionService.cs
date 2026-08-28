using System.Globalization;
using System.Text;
using DiscordBot.Notifications;

namespace DiscordBot.FantasyPremierLeague.Recognition;

public sealed class FplRecognitionMessageCompositionService
{
    private const string NoDataMessage =
        "📭 Данные о достижениях пока недоступны. Попробуйте ещё раз позже.";
    private const string TotalAwardsHeading = "🎖️ Больше всего наград";
    private const string GameweekWinsHeading = "👑 Победы в турах";
    private const string TrackingPrefix = "📍 Достижения отслеживаются с GW";

    public string ComposeProfile(FplManagerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var summary = new StringBuilder();
        summary.Append("🏅 ");
        summary.Append(DiscordTextSafety.SanitizeExternalName(profile.EntryName));
        summary.Append('\n');
        summary.Append(DiscordTextSafety.SanitizeExternalName(profile.ManagerName));
        summary.Append("\n\n📊 Сезон ");
        summary.Append(profile.Season);
        summary.Append("\n🏆 Место: ");
        summary.Append(profile.CurrentRank.ToString(CultureInfo.InvariantCulture));
        summary.Append("\n⭐ Очки: ");
        summary.Append(FormatTotal(profile.TotalScore));
        summary.Append("\n👑 Побед в турах: ");
        summary.Append(profile.GameweekWins.ToString(CultureInfo.InvariantCulture));

        summary.Append("\n\n🎖️ Достижения: ");
        summary.Append(profile.TotalAchievements.ToString(CultureInfo.InvariantCulture));
        foreach (var achievement in profile.AchievementCounts)
        {
            summary.Append('\n');
            summary.Append(FplAchievementDisplay.GetName(
                achievement.AchievementKey,
                achievement.AchievementName));
            summary.Append(" ×");
            summary.Append(achievement.Count.ToString(CultureInfo.InvariantCulture));
        }

        summary.Append("\n\n📚 Личные рекорды");
        summary.Append("\n⚡ Лучший тур: ");
        summary.Append(profile.BestGameweekScore.ToString(CultureInfo.InvariantCulture));
        summary.Append(' ');
        summary.Append(FormatPointsNoun(profile.BestGameweekScore));
        summary.Append(" — GW");
        summary.Append(profile.BestGameweekEventId.ToString(CultureInfo.InvariantCulture));
        summary.Append("\n🪑 Максимум на скамейке: ");
        summary.Append(profile.HighestBenchPoints.ToString(CultureInfo.InvariantCulture));
        summary.Append(' ');
        summary.Append(FormatPointsNoun(profile.HighestBenchPoints));
        summary.Append(" — GW");
        summary.Append(profile.HighestBenchPointsEventId.ToString(CultureInfo.InvariantCulture));

        summary.Append("\n\n");
        summary.Append(TrackingPrefix);
        summary.Append(profile.TrackingStartedEventId.ToString(CultureInfo.InvariantCulture));

        return summary.ToString();
    }

    public string ComposeSeasonSummary(FplAchievementSeasonSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var builder = new StringBuilder();
        builder.Append("🏆 Достижения — сезон ");
        builder.Append(summary.Season);

        if (summary.TotalAwardRanking.Count > 0)
        {
            builder.Append("\n\n");
            builder.Append(TotalAwardsHeading);
            AppendRankedTotals(builder, summary.TotalAwardRanking);
        }

        foreach (var category in summary.PerAchievementLeaders)
        {
            builder.Append("\n\n");
            builder.Append(FplAchievementDisplay.GetName(
                category.AchievementKey,
                category.AchievementKey));
            AppendLeaders(builder, category.Leaders, category.Count);
        }

        if (summary.GameweekWinTopCount > 0 && summary.GameweekWinLeaders.Count > 0)
        {
            builder.Append("\n\n");
            builder.Append(GameweekWinsHeading);
            AppendLeaders(builder, summary.GameweekWinLeaders, summary.GameweekWinTopCount);
        }

        builder.Append("\n\n");
        builder.Append(TrackingPrefix);
        builder.Append(summary.TrackingStartedEventId.ToString(CultureInfo.InvariantCulture));

        return builder.ToString();
    }

    public string ComposeManagerNotFound(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "🤷 Укажите название команды или имя менеджера.";
        }

        var safeQuery = DiscordTextSafety.SanitizeExternalName(query);
        return $"🤷 Не удалось найти менеджера \"{safeQuery}\".";
    }

    public string ComposeAmbiguousManager(
        string query,
        IReadOnlyList<FplManagerReference> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var builder = new StringBuilder();
        builder.Append("🤔 Нашлось несколько подходящих менеджеров:");
        foreach (var candidate in candidates.OrderBy(
                     candidate => candidate.EntryName, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("\n• ");
            builder.Append(DiscordTextSafety.SanitizeExternalName(candidate.EntryName));
            builder.Append(" — ");
            builder.Append(DiscordTextSafety.SanitizeExternalName(candidate.ManagerName));
        }

        builder.Append("\n\nУточните название команды или имя менеджера.");

        return builder.ToString();
    }

    public string ComposeNoData()
    {
        return NoDataMessage;
    }

    private static void AppendRankedTotals(
        StringBuilder builder,
        IReadOnlyList<FplAchievementTotalRankingEntry> ranking)
    {
        string? previousMedal = null;
        var previousCount = 0;
        foreach (var entry in ranking)
        {
            string? medal;
            if (previousMedal is null)
            {
                medal = "🥇";
            }
            else if (entry.TotalAwards == previousCount)
            {
                medal = previousMedal;
            }
            else
            {
                medal = previousMedal switch
                {
                    "🥇" => "🥈",
                    "🥈" => "🥉",
                    _ => null
                };
            }

            if (medal is null)
            {
                break;
            }

            previousMedal = medal;
            previousCount = entry.TotalAwards;

            builder.Append('\n');
            builder.Append(medal);
            builder.Append(' ');
            builder.Append(DiscordTextSafety.SanitizeExternalName(entry.EntryName));
            builder.Append(" — ");
            builder.Append(entry.TotalAwards.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AppendLeaders(
        StringBuilder builder,
        IReadOnlyList<FplManagerReference> leaders,
        int count)
    {
        foreach (var leader in leaders.OrderBy(
                     reference => reference.EntryName, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append('\n');
            builder.Append(DiscordTextSafety.SanitizeExternalName(leader.EntryName));
            builder.Append(" — ");
            builder.Append(count.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static string FormatTotal(int total)
    {
        return total.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', ' ');
    }

    private static string FormatPointsNoun(int count)
    {
        var mod10 = count % 10;
        var mod100 = count % 100;
        if (mod10 == 1 && mod100 != 11)
        {
            return "очко";
        }

        if (mod10 is >= 2 and <= 4 && (mod100 < 10 || mod100 >= 20))
        {
            return "очка";
        }

        return "очков";
    }
}
