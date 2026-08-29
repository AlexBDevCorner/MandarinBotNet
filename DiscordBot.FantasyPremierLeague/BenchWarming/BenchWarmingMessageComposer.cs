using System.Globalization;
using System.Text;
using DiscordBot.Notifications;

namespace DiscordBot.BenchWarming;

public sealed class BenchWarmingMessageComposer
{
    private const int TopRoundStandingsCount = 3;
    private const string LeagueTitle = "🔥 Лига Обогревателей Скамейки";

    public string ComposeRoundSummary(BenchWarmingRoundResult round)
    {
        ArgumentNullException.ThrowIfNull(round);

        var summary = new StringBuilder(
            $"{LeagueTitle} — итоги тура {round.EventId.ToString(CultureInfo.InvariantCulture)} " +
            $"(сезон {round.Season}):");

        if (round.RoundStandings.Count > 0)
        {
            summary.Append("\n\n🪑 Больше всех очков оставили на скамейке в этом туре:");
            AppendStandings(summary, round.RoundStandings.Take(TopRoundStandingsCount));

            var topBenchPlayer = round.BenchPoints
                .OrderByDescending(benchPoint => benchPoint.Points)
                .ThenBy(benchPoint => benchPoint.PlayerWebName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(benchPoint => benchPoint.Points > 0);
            if (topBenchPlayer is not null)
            {
                summary.Append(
                    $"\n\n🏅 Главный обогреватель скамейки тура: {DiscordTextSafety.SanitizeExternalName(topBenchPlayer.PlayerWebName)} — " +
                    $"{topBenchPlayer.Points.ToString(CultureInfo.InvariantCulture)} очков, греющих лавку команды " +
                    $"{DiscordTextSafety.SanitizeExternalName(topBenchPlayer.EntryName)}!");
            }
        }

        summary.Append($"\n\n📊 Общий зачёт сезона {round.Season}:");
        AppendStandings(summary, round.SeasonStandings);

        return summary.ToString();
    }

    public string ComposeSeasonStandings(string season, IEnumerable<BenchWarmingEntryStanding> standings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(season);
        ArgumentNullException.ThrowIfNull(standings);

        var summary = new StringBuilder(
            $"{LeagueTitle} (сезон {season}):");
        var count = AppendStandings(summary, standings);
        if (count == 0)
        {
            summary.Append(
                "\n✨ Пока никто не греет скамейку — очки на лавке ещё не потеряны.");
        }

        return summary.ToString();
    }

    public string ComposeSeasonOverview(BenchWarmingSeasonOverview overview)
    {
        ArgumentNullException.ThrowIfNull(overview);

        var tracking = overview.Tracking;
        var expectedRoundCount = tracking.LatestEventId - tracking.FirstEventId + 1;
        var hasGaps = tracking.TrackedRoundCount != expectedRoundCount;

        var summary = new StringBuilder(
            $"{LeagueTitle} — сезон {overview.Season}");
        summary.Append("\n📍 Статистика отслеживается с GW")
            .Append(tracking.FirstEventId.ToString(CultureInfo.InvariantCulture))
            .Append(" · учтено ")
            .Append(tracking.TrackedRoundCount.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(TourWord(tracking.TrackedRoundCount));
        if (hasGaps)
        {
            summary.Append("\n⚠️ В истории есть пропущенные туры.");
        }

        summary.Append("\n\n🪑 Последний тур — GW")
            .Append(overview.LatestEventId.ToString(CultureInfo.InvariantCulture));
        AppendStandings(summary, overview.LatestRoundStandings);

        summary.Append("\n\n📊 Общий зачёт");
        AppendStandings(summary, overview.SeasonStandings);

        summary.Append("\n\n🏆 Рекорды сезона");
        AppendSeasonRecords(summary, overview.Records);

        return summary.ToString();
    }

    public string ComposeRoundStandings(BenchWarmingRoundSummary round)
    {
        ArgumentNullException.ThrowIfNull(round);

        var summary = new StringBuilder(
            $"{LeagueTitle} — GW{round.EventId.ToString(CultureInfo.InvariantCulture)}");
        AppendStandings(summary, round.Standings);

        if (round.TopBenchPlayer is not null)
        {
            summary.Append("\n\n🏅 Главный обогреватель тура:\n")
                .Append(DiscordTextSafety.SanitizeExternalName(round.TopBenchPlayer.PlayerWebName))
                .Append(" — ")
                .Append(round.TopBenchPlayer.Points.ToString(CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(PointsWord(round.TopBenchPlayer.Points))
                .Append(" на скамейке ")
                .Append(DiscordTextSafety.SanitizeExternalName(round.TopBenchPlayer.EntryName));
        }

        return summary.ToString();
    }

    public string ComposeTeamProfile(BenchWarmingTeamProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var summary = new StringBuilder(
            $"{LeagueTitle} — ")
            .Append(DiscordTextSafety.SanitizeExternalName(profile.EntryName))
            .Append(" — скамейка ")
            .Append(profile.Season);
        summary.Append("\n📍 Данные команды с GW")
            .Append(profile.FirstTrackedEventId.ToString(CultureInfo.InvariantCulture));

        summary.Append("\n\n💺 Всего оставлено: ")
            .Append(profile.TotalPoints.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(PointsWord(profile.TotalPoints));
        summary.Append("\n📊 Среднее: ")
            .Append(profile.AveragePoints.ToString("F1", CultureInfo.InvariantCulture))
            .Append(" очка/GW");
        summary.Append("\n💥 Рекорд: ")
            .Append(profile.HighestRoundPoints.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(PointsWord(profile.HighestRoundPoints))
            .Append(" — GW")
            .Append(profile.HighestRoundEventId.ToString(CultureInfo.InvariantCulture));
        summary.Append("\n🔥 Серия 8+: ")
            .Append(profile.LongestEightPlusStreak.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(TourWord(profile.LongestEightPlusStreak));
        summary.Append("\n🧼 Лучшая чистая серия: ")
            .Append(profile.LongestCleanBenchStreak.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(TourWord(profile.LongestCleanBenchStreak));

        summary.Append("\n\nИстория:");
        foreach (var round in profile.History)
        {
            summary.Append("\nGW")
                .Append(round.EventId.ToString(CultureInfo.InvariantCulture))
                .Append(" — ")
                .Append(round.Points.ToString(CultureInfo.InvariantCulture))
                .Append(GetHistoryDecoration(profile, round));
        }

        return summary.ToString();
    }

    public string ComposeTeamNotFound(string query)
    {
        return $"🔥 Команда \"{DiscordTextSafety.SanitizeExternalName(query)}\" не найдена " +
            "в Лиге обогревателей скамейки.";
    }

    public string ComposeAmbiguousTeam(
        string query,
        IReadOnlyList<BenchWarmingTeamCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var summary = new StringBuilder(
            $"Не удалось однозначно найти команду " +
            $"\"{DiscordTextSafety.SanitizeExternalName(query)}\".");
        summary.Append("\n\nВозможные варианты:");
        foreach (var candidate in candidates)
        {
            summary.Append("\n• ")
                .Append(DiscordTextSafety.SanitizeExternalName(candidate.EntryName))
                .Append(" — ID ")
                .Append(candidate.EntryId.ToString(CultureInfo.InvariantCulture));
        }

        return summary.ToString();
    }

    public string ComposeRoundNotTracked(
        int eventId,
        BenchWarmingTrackingInfo tracking)
    {
        ArgumentNullException.ThrowIfNull(tracking);

        var header = $"🔥 Данных Лиги обогревателей за GW{eventId.ToString(CultureInfo.InvariantCulture)} нет.";

        if (eventId < tracking.FirstEventId)
        {
            return header + "\n" +
                $"📍 Отслеживание началось с GW{tracking.FirstEventId.ToString(CultureInfo.InvariantCulture)}.";
        }

        if (eventId > tracking.LatestEventId)
        {
            return header + "\n" +
                $"📍 Последний учтённый тур — GW{tracking.LatestEventId.ToString(CultureInfo.InvariantCulture)}.";
        }

        return header + "\n" +
            $"📍 Отслеживание охватывает GW{tracking.FirstEventId.ToString(CultureInfo.InvariantCulture)}" +
            $"–GW{tracking.LatestEventId.ToString(CultureInfo.InvariantCulture)} " +
            "(в этом туре данных нет).";
    }

    private static void AppendSeasonRecords(
        StringBuilder summary,
        BenchWarmingSeasonRecords records)
    {
        if (records.BiggestBenchDisaster is { } disaster)
        {
            summary.Append("\n💥 Худшая скамейка: ")
                .Append(DiscordTextSafety.SanitizeExternalName(disaster.EntryName))
                .Append(" — ")
                .Append(disaster.Points.ToString(CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(PointsWord(disaster.Points))
                .Append(" (GW")
                .Append(disaster.EventId.ToString(CultureInfo.InvariantCulture))
                .Append(')');
        }
        else
        {
            summary.Append("\n💥 Худшая скамейка: пока никто не грел скамейку");
        }

        summary.Append("\n📈 В среднем: ")
            .Append(records.AveragePointsPerManagerRound.ToString("F1", CultureInfo.InvariantCulture))
            .Append(" очка на скамейке на менеджера за тур");

        AppendStreakLine(
            summary,
            "🔥 Серия 8+:",
            records.LongestEightPlusStreak);
        AppendStreakLine(
            summary,
            "🧼 Чистая скамейка:",
            records.LongestCleanBenchStreak);
    }

    private static void AppendStreakLine(
        StringBuilder summary,
        string label,
        BenchWarmingStreakRecord? streak)
    {
        summary.Append('\n').Append(label).Append(' ');
        if (streak is null)
        {
            summary.Append("пока никто");
            return;
        }

        summary.Append(DiscordTextSafety.SanitizeExternalName(streak.EntryName))
            .Append(" — ")
            .Append(streak.Length.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(TourWord(streak.Length))
            .Append(" подряд");
    }

    private static string GetHistoryDecoration(
        BenchWarmingTeamProfile profile,
        BenchWarmingEntryRoundStanding round)
    {
        if (profile.HighestRoundPoints > 0 &&
            round.Points == profile.HighestRoundPoints)
        {
            return " 💥";
        }

        if (round.Points >= 8)
        {
            return " 🔥";
        }

        if (round.Points == 0)
        {
            return " 🧼";
        }

        return string.Empty;
    }

    private static string PointsWord(int points)
    {
        var mod10 = points % 10;
        var mod100 = points % 100;
        if (mod10 == 1 && mod100 != 11)
        {
            return "очко";
        }

        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20))
        {
            return "очка";
        }

        return "очков";
    }

    private static string TourWord(int count)
    {
        var mod10 = count % 10;
        var mod100 = count % 100;
        if (mod10 == 1 && mod100 != 11)
        {
            return "тур";
        }

        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20))
        {
            return "тура";
        }

        return "туров";
    }

    private static int AppendStandings(
        StringBuilder summary,
        IEnumerable<BenchWarmingEntryStanding> standings)
    {
        var count = 0;
        foreach (var standing in standings)
        {
            summary.Append('\n');
            summary.Append(
                $"{GetRankLabel(count + 1)} {DiscordTextSafety.SanitizeExternalName(standing.EntryName)} — " +
                $"{standing.Points.ToString(CultureInfo.InvariantCulture)}");
            count++;
        }

        return count;
    }

    private static string GetRankLabel(int rank) => rank switch
    {
        1 => ":one:",
        2 => ":two:",
        3 => ":three:",
        _ => $"{rank.ToString(CultureInfo.InvariantCulture)}."
    };
}
