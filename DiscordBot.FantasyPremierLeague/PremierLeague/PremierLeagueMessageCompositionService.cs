using System.Globalization;
using System.Text;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recap;
using DiscordBot.FantasyPremierLeague.Recognition;
using DiscordBot.FantasyPremierLeague.Standings;
using DiscordBot.Notifications;
using DiscordBot.Responses;

namespace DiscordBot.PremierLeague;

public sealed class PremierLeagueMessageCompositionService(
    ConfiguredTimeZone configuredTimeZone)
{
    public const int ClassicCongratulationsVariantCount = 31;

    private const string ClassicSnapshotHeading =
        "🏆 Турнирная таблица классической лиги:";
    private const string ClassicAroundHeadingPrefix =
        "🏆 Классическая лига — вокруг ";
    private const string HeadToHeadSnapshotHeading =
        "⚔️ Турнирная таблица лиги один на один:";
    private const string EmptyStandingsMessage =
        "🤷 Данные о позициях не получены.";
    private const string UnavailableStandingsMessage =
        "⚠️ Эта лига сейчас недоступна.";
    private const string AroundTargetMarker = "👉 ";

    public string ComposeDeadlineReminder(
        DateTimeOffset deadlineUtc,
        DateTimeOffset utcNow)
    {
        _ = configuredTimeZone;
        _ = utcNow;
        var absoluteTimestamp = DiscordTimestamp.FormatAbsolute(deadlineUtc);
        var relativeTimestamp = DiscordTimestamp.FormatRelative(deadlineUtc);

        return "Привет мои любители АПЛ и обнимашек! :people_hugging: Следующий тур уже скоро!" +
            $"\nДедлайн: {absoluteTimestamp}" +
            $"\nОсталось: {relativeTimestamp}";
    }

    public string ComposeClassicStandings(
        IEnumerable<ClassicStanding> standings,
        IEnumerable<ClassicStanding> eventWinners,
        IEnumerable<StandingsChange> changes,
        int congratulationsVariant)
    {
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(eventWinners);
        ArgumentNullException.ThrowIfNull(changes);

        var winners = eventWinners.ToArray();
        var summary = new StringBuilder("🏆 Лига Пельменных Обнимашек:");

        AppendStandings(
            summary,
            standings,
            result => result.Rank,
            result => result.EntryName,
            result => result.Total);

        if (winners.Length == 1)
        {
            var eventWinner = winners[0];
            var safeEventWinner = new ClassicStanding
            {
                EntryName = DiscordTextSafety.SanitizeExternalName(
                    eventWinner.EntryName),
                EventTotal = eventWinner.EventTotal
            };
            summary.Append(GetCongratulationsMessages(safeEventWinner)[congratulationsVariant]);
        }
        else if (winners.Length > 1)
        {
            var winnerNames = string.Join(
                ", ",
                winners.Select(winner =>
                    DiscordTextSafety.SanitizeExternalName(winner.EntryName)));
            summary.Append(
                CultureInfo.InvariantCulture,
                $"\n\nВ этом туре максимум очков ({winners[0].EventTotal}) разделили команды: {winnerNames}. Обнимашки всем победителям! :people_hugging:");
        }

        foreach (var change in changes)
        {
            summary.Append('\n');

            if (change.Direction == StandingsChangeDirection.Up)
            {
                summary.Append(CultureInfo.InvariantCulture, $"Команда {DiscordTextSafety.SanitizeExternalName(change.EntryName)} смогла взобраться на {change.PositionCount} позиции вверх :arrow_up:, поздравительные обнимашки! :people_hugging: Так держать!");
            }
            else
            {
                summary.Append(CultureInfo.InvariantCulture, $"Команда {DiscordTextSafety.SanitizeExternalName(change.EntryName)} упала на {change.PositionCount} позиции вниз :arrow_down:, обнимашки поддержки! :people_hugging: Всё наладится!");
            }
        }

        return summary.ToString();
    }

    public string ComposeHeadToHeadStandings(
        IEnumerable<HeadToHeadStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        var summary = new StringBuilder(
            "⚔️ Лига Пельменных Обнимашек-К-Обнимашкам:");

        AppendStandings(
            summary,
            standings,
            result => result.Rank,
            result => result.EntryName,
            result => result.Total);

        return summary.ToString();
    }

    public string ComposeGameweekRecap(FplGameweekRecap recap)
    {
        ArgumentNullException.ThrowIfNull(recap);

        var summary = new StringBuilder(
            $"📊 GW{recap.EventId.ToString(CultureInfo.InvariantCulture)} — что произошло");

        foreach (var highlight in recap.Highlights)
        {
            summary.Append('\n');
            AppendHighlight(summary, highlight);
        }

        if (recap.SeasonTrends.Count > 0)
        {
            summary.Append("\n\n📈 Сюжет сезона");
            foreach (var trend in recap.SeasonTrends)
            {
                AppendTrend(summary, trend);
            }
        }

        if (recap.Standings.Count > 0)
        {
            summary.Append("\n\n🏆 Таблица");
            AppendStandings(summary, recap.Standings);
        }

        if (recap.Achievements.Count > 0)
        {
            var visibleAchievements = recap.Achievements
                .Where(award => !RetiredFplAchievementKeys.IsRetired(award.AchievementKey))
                .ToArray();
            if (visibleAchievements.Length > 0)
            {
                summary.Append("\n\n🎖️ Достижения");
                AppendAchievements(summary, visibleAchievements);
            }
        }

        return summary.ToString();
    }

    public string ComposeClassicStandingsSnapshot(
        FplClassicStandingsView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var summary = new StringBuilder(ClassicSnapshotHeading);
        AppendClassicRows(summary, view.Rows);
        return FinishSnapshot(summary);
    }

    public string ComposeClassicAroundStandings(
        FplAroundLookupResult result,
        string query)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(query);

        var heading = ClassicAroundHeadingPrefix +
            DiscordTextSafety.SanitizeExternalName(query);
        var summary = new StringBuilder(heading);
        AppendClassicRows(summary, result.Rows);
        return FinishSnapshot(summary);
    }

    public string ComposeHeadToHeadStandingsSnapshot(
        FplHeadToHeadStandingsView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var summary = new StringBuilder(HeadToHeadSnapshotHeading);
        AppendHeadToHeadRows(summary, view.Rows);
        return FinishSnapshot(summary);
    }

    public string ComposeManagerNotFound(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return
            $"🤷 Не удалось найти менеджера или команду \"{DiscordTextSafety.SanitizeExternalName(query)}\".";
    }

    public string ComposeAmbiguousManager(
        string query,
        IReadOnlyList<FplClassicCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);

        var summary = new StringBuilder(
            $"🤔 Нашлось несколько вариантов для \"{DiscordTextSafety.SanitizeExternalName(query)}\":");
        foreach (var candidate in candidates)
        {
            summary.Append("\n• ");
            summary.Append(DiscordTextSafety.SanitizeExternalName(candidate.EntryName));
            summary.Append(" — ");
            summary.Append(DiscordTextSafety.SanitizeExternalName(candidate.ManagerName));
        }

        summary.Append("\n\nУточните название команды или имя менеджера.");
        return summary.ToString();
    }

    private static string FinishSnapshot(StringBuilder summary)
    {
        if (summary.ToString().IndexOf('\n') < 0)
        {
            summary.Append('\n');
            summary.Append(EmptyStandingsMessage);
        }

        return summary.ToString();
    }

    private static void AppendClassicRows(
        StringBuilder summary,
        IReadOnlyList<FplClassicStandingRow> rows)
    {
        foreach (var row in rows)
        {
            summary.Append('\n');
            if (row.IsAroundTarget)
            {
                summary.Append(AroundTargetMarker);
            }

            summary.Append(GetStandingsRankLabel(row.Rank));
            summary.Append(' ');
            summary.Append(DiscordTextSafety.SanitizeExternalName(row.EntryName));
            summary.Append(" — ");
            summary.Append(FormatTotal(row.TotalPoints));
            summary.Append(" | GW ");
            summary.Append(row.GameweekPoints.ToString(CultureInfo.InvariantCulture));
            AppendMovement(summary, row.Movement, row.MovementDelta);
            AppendGap(summary, row.HasGap, row.GapToLeader);
        }
    }

    private static void AppendHeadToHeadRows(
        StringBuilder summary,
        IReadOnlyList<FplHeadToHeadStandingRow> rows)
    {
        foreach (var row in rows)
        {
            summary.Append('\n');
            summary.Append(GetStandingsRankLabel(row.Rank));
            summary.Append(' ');
            summary.Append(DiscordTextSafety.SanitizeExternalName(row.EntryName));
            summary.Append(" — ");
            summary.Append(FormatTotal(row.TotalPoints));
            AppendMovement(summary, row.Movement, row.MovementDelta);
            summary.Append(" | ");
            summary.Append(row.MatchesPlayed.ToString(CultureInfo.InvariantCulture));
            summary.Append(' ');
            summary.Append(FormatMatchesNoun(row.MatchesPlayed));
            AppendGap(summary, row.HasGap, row.GapToLeader);
        }
    }

    private static void AppendMovement(
        StringBuilder summary,
        FplStandingsMovement movement,
        int delta)
    {
        if (movement == FplStandingsMovement.None || delta <= 0)
        {
            return;
        }

        var arrow = movement == FplStandingsMovement.Up ? '↑' : '↓';
        summary.Append(' ');
        summary.Append(arrow);
        summary.Append(delta.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendGap(
        StringBuilder summary,
        bool hasGap,
        int gap)
    {
        if (!hasGap)
        {
            return;
        }

        summary.Append(" | ");
        summary.Append(gap.ToString(CultureInfo.InvariantCulture));
        summary.Append(" до лидера");
    }

    public static string ComposeUnavailableClassicStandings()
    {
        return $"{ClassicSnapshotHeading}\n{UnavailableStandingsMessage}";
    }

    public static string ComposeUnavailableHeadToHeadStandings()
    {
        return $"{HeadToHeadSnapshotHeading}\n{UnavailableStandingsMessage}";
    }

    private static int AppendStandings<T>(
        StringBuilder summary,
        IEnumerable<T> standings,
        Func<T, int> rank,
        Func<T, string?> entryName,
        Func<T, int> total)
    {
        var count = 0;
        foreach (var result in standings)
        {
            summary.Append('\n');
            summary.Append(
                CultureInfo.InvariantCulture,
                $"{GetRankLabel(rank(result))} {DiscordTextSafety.SanitizeExternalName(entryName(result))} {total(result)}");
            count++;
        }

        return count;
    }

    private static void AppendHighlight(StringBuilder summary, FplRecapHighlight highlight)
    {
        switch (highlight)
        {
            case FplGameweekWinnerHighlight winner:
                AppendWinner(summary, winner);
                break;
            case FplRankMovementHighlight movement:
                summary.Append(
                    movement.Kind == FplRecapHighlightKind.BiggestClimb
                        ? "🚀 "
                        : "🪂 ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    movement.Manager.EntryName));
                summary.Append(
                    movement.Kind == FplRecapHighlightKind.BiggestClimb
                        ? " взлетел с "
                        : " упал с ");
                summary.Append(OrdinalGenitive(movement.PreviousRank));
                summary.Append(" на ");
                summary.Append(OrdinalPlace(movement.CurrentRank));
                break;
            case FplCaptainDisasterHighlight captainDisaster:
                summary.Append("💥 ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    captainDisaster.Manager.EntryName));
                summary.Append(": капитан ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    captainDisaster.Captain.PlayerName));
                summary.Append(
                    $" — {captainDisaster.Captain.Points.ToString(CultureInfo.InvariantCulture)}, VC ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    captainDisaster.ViceCaptain.PlayerName));
                summary.Append(
                    $" — {captainDisaster.ViceCaptain.Points.ToString(CultureInfo.InvariantCulture)}");
                break;
            case FplTransferHitHighlight transferHit:
                summary.Append("💸 ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    transferHit.Manager.EntryName));
                summary.Append(
                    $" взял -{transferHit.TransferCost.ToString(CultureInfo.InvariantCulture)} " +
                    "за трансферы и закончил тур с " +
                    $"{transferHit.Manager.EventScore.ToString(CultureInfo.InvariantCulture)} " +
                    $"{FormatPointsInstrumental(transferHit.Manager.EventScore)}");
                break;
            case FplBenchDisasterHighlight benchDisaster:
                summary.Append("🪑 ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    benchDisaster.Manager.EntryName));
                summary.Append(
                    $" оставил {benchDisaster.Manager.BenchPoints.ToString(CultureInfo.InvariantCulture)} " +
                    $"{FormatPointsNoun(benchDisaster.Manager.BenchPoints)} на скамейке");
                break;
        }
    }

    private static void AppendWinner(
        StringBuilder summary,
        FplGameweekWinnerHighlight highlight)
    {
        summary.Append("👑 ");
        summary.Append(DiscordTextSafety.SanitizeExternalName(
            highlight.Winners[0].EntryName));
        if (highlight.Winners.Count > 1)
        {
            for (var i = 1; i < highlight.Winners.Count; i++)
            {
                summary.Append(i == highlight.Winners.Count - 1 ? " и " : ", ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    highlight.Winners[i].EntryName));
            }

            summary.Append(" разделили победу в туре");
        }
        else
        {
            summary.Append(" выиграл тур");
        }

        summary.Append(
            $" — {highlight.Score.ToString(CultureInfo.InvariantCulture)} " +
            $"{FormatPointsNoun(highlight.Score)}");
    }

    private static void AppendTrend(StringBuilder summary, FplSeasonTrend trend)
    {
        switch (trend)
        {
            case FplFirstTimeAtTopTrend firstTime:
                summary.Append("\n👑 ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    firstTime.Leader.EntryName));
                summary.Append(
                    firstTime.SinceTrackingStarted
                        ? " впервые с начала отслеживания вышел на первое место"
                        : " впервые вышел на первое место");
                break;
            case FplRecentWinsTrend recentWins:
                summary.Append("\n🔥 ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    recentWins.Manager.EntryName));
                summary.Append(
                    $" — {recentWins.WinCount.ToString(CultureInfo.InvariantCulture)} " +
                    $"{FormatWinsNoun(recentWins.WinCount)} за последние " +
                    $"{recentWins.Window.ToString(CultureInfo.InvariantCulture)} " +
                    $"{FormatTourNoun(recentWins.Window)}");
                break;
            case FplConsecutiveRankRisesTrend rises:
                summary.Append("\n⬆️ ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    rises.Manager.EntryName));
                summary.Append(
                    $" поднимается в таблице " +
                    $"{rises.StreakLength.ToString(CultureInfo.InvariantCulture)} " +
                    $"{FormatTourNoun(rises.StreakLength)} подряд");
                break;
            case FplLeaderGapReductionTrend gap:
                summary.Append("\n🎯 ");
                summary.Append(DiscordTextSafety.SanitizeExternalName(
                    gap.Manager.EntryName));
                summary.Append(
                    $" сократил отставание от лидера с " +
                    $"{gap.PreviousGap.ToString(CultureInfo.InvariantCulture)} до " +
                    $"{gap.CurrentGap.ToString(CultureInfo.InvariantCulture)} " +
                    $"{FormatPointsNoun(gap.CurrentGap)}");
                break;
        }
    }

    private static void AppendStandings(
        StringBuilder summary,
        IReadOnlyList<FplManagerGameweekStatistics> standings)
    {
        if (standings.Count == 0)
        {
            return;
        }

        var leaderTotal = standings[0].TotalScore;
        foreach (var manager in standings)
        {
            summary.Append('\n');
            summary.Append(GetStandingsRankLabel(manager.Rank));
            summary.Append(' ');
            summary.Append(DiscordTextSafety.SanitizeExternalName(manager.EntryName));
            summary.Append(" — ");
            summary.Append(FormatTotal(manager.TotalScore));

            if (manager.LastRank > 0 && manager.Rank != manager.LastRank)
            {
                var delta = Math.Abs(manager.Rank - manager.LastRank);
                var arrow = manager.Rank < manager.LastRank ? '↑' : '↓';
                summary.Append(
                    $" {arrow}{delta.ToString(CultureInfo.InvariantCulture)}");
            }

            if (manager.Rank != 1)
            {
                var gap = leaderTotal - manager.TotalScore;
                summary.Append($" — {gap.ToString(CultureInfo.InvariantCulture)} до лидера");
            }
        }
    }

    private static void AppendAchievements(
        StringBuilder summary,
        IEnumerable<FplAchievementAward> achievements)
    {
        foreach (var award in achievements)
        {
            if (RetiredFplAchievementKeys.IsRetired(award.AchievementKey))
            {
                continue;
            }

            summary.Append("\n");
            summary.Append(DiscordTextSafety.SanitizeExternalName(award.EntryName));
            summary.Append(" — ");
            summary.Append(FplAchievementDisplay.GetName(
                award.AchievementKey,
                award.AchievementName));
        }
    }

    private static string GetStandingsRankLabel(int rank) => rank switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => $"{rank.ToString(CultureInfo.InvariantCulture)}."
    };

    private static string GetRankLabel(int rank) => rank switch
    {
        1 => ":one:",
        2 => ":two:",
        3 => ":three:",
        _ => $"{rank.ToString(CultureInfo.InvariantCulture)}."
    };

    private static string FormatTotal(int total)
    {
        return total.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', ' ');
    }

    private static string OrdinalGenitive(int rank)
    {
        return $"{rank.ToString(CultureInfo.InvariantCulture)}-го";
    }

    private static string OrdinalPlace(int rank)
    {
        return $"{rank.ToString(CultureInfo.InvariantCulture)}-е место";
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

    private static string FormatPointsInstrumental(int count)
    {
        var mod10 = count % 10;
        var mod100 = count % 100;
        if (mod10 == 1 && mod100 != 11)
        {
            return "очком";
        }

        return "очками";
    }

    private static string FormatWinsNoun(int count)
    {
        var mod10 = count % 10;
        var mod100 = count % 100;
        if (mod10 == 1 && mod100 != 11)
        {
            return "победа";
        }

        if (mod10 is >= 2 and <= 4 && (mod100 < 10 || mod100 >= 20))
        {
            return "победы";
        }

        return "побед";
    }

    private static string FormatTourNoun(int count)
    {
        var mod10 = count % 10;
        var mod100 = count % 100;
        if (mod10 == 1 && mod100 != 11)
        {
            return "тур";
        }

        if (mod10 is >= 2 and <= 4 && (mod100 < 10 || mod100 >= 20))
        {
            return "тура";
        }

        return "туров";
    }

    private static string FormatMatchesNoun(int count)
    {
        var mod10 = count % 10;
        var mod100 = count % 100;
        if (mod10 == 1 && mod100 != 11)
        {
            return "матч";
        }

        if (mod10 is >= 2 and <= 4 && (mod100 < 10 || mod100 >= 20))
        {
            return "матча";
        }

        return "матчей";
    }

    private static IReadOnlyList<string> GetCongratulationsMessages(
        ClassicStanding eventWinner)
    {
        var entryName = DiscordTextSafety.SanitizeExternalName(
            eventWinner.EntryName);

        return
        [
            $"\n\nВ последнем туре больше всех баллов набрала команда {entryName} - {eventWinner.EventTotal}, это заслуживает обнимашек! :people_hugging:",
            $"\n\nКоманда {eventWinner.EntryName} набрала больше всех баллов в последнем туре - {eventWinner.EventTotal}! Заслуженные обнимашки летят к вам! :people_hugging:",
            $"\n\nБольше всех баллов в этом туре({eventWinner.EventTotal}) заработала команда {eventWinner.EntryName}. Ваши обнимашки уже в пути! :people_hugging:",
            $"\n\nВ последнем раунде команда {eventWinner.EntryName} взяла больше всех баллов - {eventWinner.EventTotal}! Обнимашки для вас! :people_hugging:",
            $"\n\nПоздравляем {eventWinner.EntryName} с наибольшим количеством очков в туре - {eventWinner.EventTotal}! В честь этого – обнимашки! :people_hugging:",
            $"\n\nКоманда {eventWinner.EntryName} снова впереди с максимальными баллами в туре - {eventWinner.EventTotal}! Обнимашки ждут! :people_hugging:",
            $"\n\n{eventWinner.EntryName} заработала больше всех баллов в этом раунде - {eventWinner.EventTotal}! Обнимашки заслужены на 100%! :people_hugging:",
            $"\n\nБольше всех очков в последнем туре({eventWinner.EventTotal}) у команды {eventWinner.EntryName}. За это – порция обнимашек! :people_hugging:",
            $"\n\nУра, {eventWinner.EntryName} заработала больше всех очков - {eventWinner.EventTotal}! Держите свои обнимашки! :people_hugging:",
            $"\n\n{eventWinner.EntryName} в этом туре набрала максимум баллов - {eventWinner.EventTotal}! Обнимашки для чемпионов! :people_hugging:",
            $"\n\nОбнимашки :people_hugging: для {eventWinner.EntryName} – они набрали больше всех баллов в этом туре({eventWinner.EventTotal})!",
            $"\n\nВ туре впереди всех команда {eventWinner.EntryName} с самыми высокими баллами - {eventWinner.EventTotal}! Обнимашки заслужены! :people_hugging:",
            $"\n\nПоздравляем команду {eventWinner.EntryName} с рекордом по баллам в последнем раунде - {eventWinner.EventTotal}! Обнимашки от всех нас! :people_hugging:",
            $"\n\nКоманда {eventWinner.EntryName} снова впереди! Максимум баллов({eventWinner.EventTotal}) – обнимашки отправлены! :people_hugging:",
            $"\n\nУ команды {eventWinner.EntryName} больше всех баллов в туре - {eventWinner.EventTotal}! Настало время для обнимашек! :people_hugging:",
            $"\n\nВ этом раунде {eventWinner.EntryName} не только набрала больше всех очков - {eventWinner.EventTotal}, но и заслужила обнимашки! :people_hugging:",
            $"\n\n{eventWinner.EntryName} набрала больше всех баллов в туре - {eventWinner.EventTotal}! Ловите заслуженные обнимашки! :people_hugging:",
            $"\n\nКоманда {eventWinner.EntryName} победила по баллам в этом туре - {eventWinner.EventTotal}! Обнимашки летят к вам! :people_hugging:",
            $"\n\nСамая результативная команда тура({eventWinner.EventTotal}) – {eventWinner.EntryName}! Ваша награда – обнимашки! :people_hugging:",
            $"\n\nЗаслуженные обнимашки :people_hugging: для {eventWinner.EntryName}, которая взяла больше всех баллов в этом туре - {eventWinner.EventTotal}!",
            $"\n\nВ этом туре команда {eventWinner.EntryName} впереди с максимальными баллами - {eventWinner.EventTotal}! Обнимашки всем игрокам! :people_hugging:",
            $"\n\n{eventWinner.EntryName} набрала больше всех баллов в туре({eventWinner.EventTotal}) – обнимашки за такую отличную игру! :people_hugging:",
            $"\n\nВпечатляющие баллы от {eventWinner.EntryName} в этом раунде - {eventWinner.EventTotal}! Получите свои обнимашки! :people_hugging:",
            $"\n\nКоманда {eventWinner.EntryName} – чемпион этого тура по баллам({eventWinner.EventTotal})! Держите обнимашки! :people_hugging:",
            $"\n\nБольше всех баллов в этом туре({eventWinner.EventTotal}) – у {eventWinner.EntryName}! За это – огромные обнимашки! :people_hugging:",
            $"\n\n{eventWinner.EntryName} взяла больше всех очков в раунде - {eventWinner.EventTotal}! Обнимашки заслужены на 100%! :people_hugging:",
            $"\n\nМощный результат от {eventWinner.EntryName} – больше всех баллов({eventWinner.EventTotal})! Обнимашки ждут вас! :people_hugging:",
            $"\n\n{eventWinner.EntryName} в этом туре заработала больше всех очков - {eventWinner.EventTotal}! Заслуженные обнимашки уже летят! :people_hugging:",
            $"\n\nЛидеры тура – {eventWinner.EntryName} с наибольшим количеством очков({eventWinner.EventTotal})! Обнимашки для победителей! :people_hugging:",
            $"\n\nМаксимум очков({eventWinner.EventTotal}) у команды {eventWinner.EntryName} в этом туре – поздравляем с обнимашками! :people_hugging:",
            $"\n\nВпечатляющие результаты {eventWinner.EntryName} – больше всех баллов в туре({eventWinner.EventTotal}) и заслуженные обнимашки! :people_hugging:"
        ];
    }
}
