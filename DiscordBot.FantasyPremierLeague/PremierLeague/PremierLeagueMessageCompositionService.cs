using System.Globalization;
using System.Text;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recap;
using DiscordBot.FantasyPremierLeague.Recognition;
using DiscordBot.Notifications;
using DiscordBot.Responses;

namespace DiscordBot.PremierLeague;

public sealed class PremierLeagueMessageCompositionService(
    ConfiguredTimeZone configuredTimeZone)
{
    public const int ClassicCongratulationsVariantCount = 31;

    private const string ClassicSnapshotHeading =
        "🏆 Турнирная таблица классической лиги:";
    private const string HeadToHeadSnapshotHeading =
        "⚔️ Турнирная таблица лиги один на один:";
    private const string EmptyStandingsMessage =
        "🤷 Данные о позициях не получены.";
    private const string UnavailableStandingsMessage =
        "⚠️ Эта лига сейчас недоступна.";

    private static readonly CultureInfo RussianCulture = new("ru-RU");

    public string ComposeDeadlineReminder(
        DateTimeOffset deadlineUtc,
        DateTimeOffset utcNow)
    {
        var localDeadline = configuredTimeZone.FromUtc(deadlineUtc);
        var remaining = deadlineUtc - utcNow;

        return "Привет мои любители АПЛ и обнимашек! :people_hugging: Следующий тур уже скоро -" +
            $" {localDeadline.ToString("dd MMMM yyyy, HH:mm", RussianCulture)}, это {localDeadline.ToString("dddd", RussianCulture)}" +
            $". До этого момента осталось всего {DateTimeUtility.GenerateRemainingDaysMessageInRussian(remaining)}.";
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
                $"\n\nВ этом туре максимум очков ({winners[0].EventTotal}) разделили команды: {winnerNames}. Обнимашки всем победителям! :people_hugging:");
        }

        foreach (var change in changes)
        {
            summary.Append('\n');

            if (change.Direction == StandingsChangeDirection.Up)
            {
                summary.Append($"Команда {DiscordTextSafety.SanitizeExternalName(change.EntryName)} смогла взобраться на {change.PositionCount} позиции вверх :arrow_up:, поздравительные обнимашки! :people_hugging: Так держать!");
            }
            else
            {
                summary.Append($"Команда {DiscordTextSafety.SanitizeExternalName(change.EntryName)} упала на {change.PositionCount} позиции вниз :arrow_down:, обнимашки поддержки! :people_hugging: Всё наладится!");
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
            $"📊 Лига Пельменных Обнимашек — итоги тура " +
            $"{recap.EventId.ToString(CultureInfo.InvariantCulture)} " +
            $"(сезон {DiscordTextSafety.SanitizeExternalName(recap.Season)}):");

        summary.Append("\n\n👑 Победитель тура: ");
        summary.Append(FormatManagersWithScore(
            recap.HighestScorers,
            recap.HighestScore));
        summary.Append("\n🚀 Лучший результат: ");
        summary.Append(FormatManagersWithScore(
            recap.HighestScorers,
            recap.HighestScore));
        summary.Append("\n🫣 Худший результат: ");
        summary.Append(FormatManagersWithScore(
            recap.LowestScorers,
            recap.LowestScore));
        summary.Append("\n📈 Средний балл лиги: ");
        summary.Append(recap.AverageScore.ToString("0.0", CultureInfo.InvariantCulture));
        summary.Append("\n🧗 Главный взлёт: ");
        summary.Append(FormatRankChanges(recap.BiggestClimbers, recap.BiggestClimb));
        summary.Append("\n🪂 Главное падение: ");
        summary.Append(FormatRankChanges(recap.BiggestFallers, recap.BiggestFall));

        summary.Append("\n\n🔄 Заметные изменения позиций:");
        if (recap.NotableRankChanges.Count == 0)
        {
            summary.Append("\n(нет заметных изменений)");
        }
        else
        {
            foreach (var manager in recap.NotableRankChanges)
            {
                summary.Append("\n");
                summary.Append(FormatRankChange(manager));
            }
        }

        summary.Append("\n\n🏆 Номинации:");
        summary.Append("\n👑 Победитель тура: ");
        summary.Append(FormatManagersWithScore(
            recap.HighestScorers,
            recap.HighestScore));
        summary.Append("\n🤡 Фрод тура: ");
        summary.Append(FormatManagersWithScore(
            recap.LowestScorers,
            recap.LowestScore));
        summary.Append("\n🪑 Повелитель скамейки: ");
        summary.Append(FormatManagersWithScore(
            recap.Benchmasters,
            manager => manager.BenchPoints));
        summary.Append("\n🧠 Капитанский гений: ");
        summary.Append(FormatCaptainPerformances(recap.CaptainGeniuses));

        summary.Append("\n\n🎖️ Достижения:");
        AppendAchievements(summary, recap.Achievements);

        return summary.ToString();
    }

    public string ComposeClassicStandingsSnapshot(
        IEnumerable<ClassicStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        return ComposeStandingsSnapshot(
            ClassicSnapshotHeading,
            standings,
            result => result.Rank,
            result => result.EntryName,
            result => result.Total);
    }

    public string ComposeHeadToHeadStandingsSnapshot(
        IEnumerable<HeadToHeadStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        return ComposeStandingsSnapshot(
            HeadToHeadSnapshotHeading,
            standings,
            result => result.Rank,
            result => result.EntryName,
            result => result.Total);
    }

    public static string ComposeUnavailableClassicStandings()
    {
        return $"{ClassicSnapshotHeading}\n{UnavailableStandingsMessage}";
    }

    public static string ComposeUnavailableHeadToHeadStandings()
    {
        return $"{HeadToHeadSnapshotHeading}\n{UnavailableStandingsMessage}";
    }

    private static string ComposeStandingsSnapshot<T>(
        string heading,
        IEnumerable<T> standings,
        Func<T, int> rank,
        Func<T, string?> entryName,
        Func<T, int> total)
    {
        var summary = new StringBuilder(heading);
        var count = AppendStandings(summary, standings, rank, entryName, total);
        if (count == 0)
        {
            summary.Append('\n');
            summary.Append(EmptyStandingsMessage);
        }

        return summary.ToString();
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
                $"{GetRankLabel(rank(result))} {DiscordTextSafety.SanitizeExternalName(entryName(result))} {total(result)}");
            count++;
        }

        return count;
    }

    private static string FormatManagersWithScore(
        IEnumerable<FplManagerGameweekStatistics> managers,
        int score)
    {
        return FormatManagersWithScore(managers, _ => score);
    }

    private static string FormatManagersWithScore(
        IEnumerable<FplManagerGameweekStatistics> managers,
        Func<FplManagerGameweekStatistics, int> score)
    {
        var formattedManagers = managers
            .Select(manager =>
                $"{DiscordTextSafety.SanitizeExternalName(manager.EntryName)} — " +
                $"{score(manager).ToString(CultureInfo.InvariantCulture)} очков")
            .ToArray();

        return formattedManagers.Length == 0
            ? "(нет данных)"
            : string.Join(", ", formattedManagers);
    }

    private static string FormatRankChanges(
        IEnumerable<FplManagerGameweekStatistics> managers,
        int rankChange)
    {
        if (rankChange == 0)
        {
            return "(нет изменений)";
        }

        return string.Join(
            ", ",
            managers.Select(manager =>
                $"{DiscordTextSafety.SanitizeExternalName(manager.EntryName)} " +
                $"({FormatSignedNumber(rankChange)})"));
    }

    private static string FormatRankChange(FplManagerGameweekStatistics manager)
    {
        return $"{DiscordTextSafety.SanitizeExternalName(manager.EntryName)} " +
            $"({FormatSignedNumber(manager.RankChange)})";
    }

    private static string FormatCaptainPerformances(
        IEnumerable<FplCaptainPerformance> performances)
    {
        var formattedPerformances = performances
            .Select(performance =>
                $"{DiscordTextSafety.SanitizeExternalName(performance.Manager.EntryName)} " +
                $"({DiscordTextSafety.SanitizeExternalName(performance.Captain.PlayerName)}, " +
                $"{performance.EffectivePoints.ToString(CultureInfo.InvariantCulture)} очков)")
            .ToArray();

        return formattedPerformances.Length == 0
            ? "(нет данных)"
            : string.Join(", ", formattedPerformances);
    }

    private static void AppendAchievements(
        StringBuilder summary,
        IEnumerable<FplAchievementAward> achievements)
    {
        var awards = achievements.ToArray();
        if (awards.Length == 0)
        {
            summary.Append("\n(пока нет)");
            return;
        }

        foreach (var award in awards)
        {
            summary.Append("\n");
            summary.Append(DiscordTextSafety.SanitizeExternalName(award.EntryName));
            summary.Append(" — ");
            summary.Append(GetLocalizedAchievementName(award));
        }
    }

    private static string GetLocalizedAchievementName(FplAchievementAward award)
    {
        return award.AchievementKey switch
        {
            "first-blood" => "🩸 Первая кровь",
            "bench-warmer" => "🔥 Обогреватель скамейки",
            "captain-disaster" => "💥 Капитанская катастрофа",
            "differential-merchant" => "💎 Повелитель дифференциалов",
            "minus-eight-enjoyer" => "💸 Любитель минус восьми",
            _ => DiscordTextSafety.SanitizeExternalName(award.AchievementName)
        };
    }

    private static string FormatSignedNumber(int value)
    {
        return value > 0
            ? $"+{value.ToString(CultureInfo.InvariantCulture)}"
            : value.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetRankLabel(int rank) => rank switch
    {
        1 => ":one:",
        2 => ":two:",
        3 => ":three:",
        _ => $"{rank.ToString(CultureInfo.InvariantCulture)}."
    };

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
