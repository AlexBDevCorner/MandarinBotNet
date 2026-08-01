using System.Globalization;
using System.Text;
using DiscordBot.Responses;

namespace DiscordBot.PremierLeague;

public sealed class PremierLeagueMessageCompositionService
{
    public const int ClassicCongratulationsVariantCount = 31;

    private static readonly CultureInfo RussianCulture = new("ru-RU");

    public string ComposeDeadlineReminder(
        DateTimeOffset deadlineUtc,
        DateTimeOffset utcNow)
    {
        var deadlineInRiga = RigaTime.FromUtc(deadlineUtc);
        var remaining = deadlineUtc - utcNow;

        return "@everyone Привет мои любители АПЛ и обнимашек! :people_hugging: Следующий тур уже скоро -" +
            $" {deadlineInRiga.ToString("dd MMMM yyyy, HH:mm", RussianCulture)}, это {deadlineInRiga.ToString("dddd", RussianCulture)}" +
            $". До этого момента осталось всего {DateTimeUtility.GenerateRemainingDaysMessageInRussian(remaining)}.";
    }

    public string ComposeClassicStandings(
        IEnumerable<ClassicStanding> standings,
        ClassicStanding? eventWinner,
        IEnumerable<StandingsChange> changes,
        int congratulationsVariant)
    {
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(changes);

        var summary = new StringBuilder("@everyone Лига Пельменных Обнимашек:");

        foreach (var result in standings)
        {
            summary.Append('\n');
            summary.Append($"{GetRankEmoji(result.Rank)} {result.EntryName} {result.Total}");
        }

        if (eventWinner is not null)
        {
            summary.Append(GetCongratulationsMessages(eventWinner)[congratulationsVariant]);
        }

        foreach (var change in changes)
        {
            summary.Append('\n');

            if (change.Direction == StandingsChangeDirection.Up)
            {
                summary.Append($"Команда {change.EntryName} смогла взобраться на {change.PositionCount} позиции вверх :arrow_up:, поздравительные обнимашки! :people_hugging: Так держать!");
            }
            else
            {
                summary.Append($"Команда {change.EntryName} упала на {change.PositionCount} позиции вниз :arrow_down:, обнимашки поддержки! :people_hugging: Всё наладится!");
            }
        }

        return summary.ToString();
    }

    public string ComposeHeadToHeadStandings(
        IEnumerable<HeadToHeadStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        var summary = new StringBuilder(
            "@everyone Лига Пельменных Обнимашек-К-Обнимашкам:");

        foreach (var result in standings)
        {
            summary.Append('\n');
            summary.Append($"{GetRankEmoji(result.Rank)} {result.EntryName} {result.Total}");
        }

        return summary.ToString();
    }

    private static string GetRankEmoji(int rank) => rank switch
    {
        1 => ":one:",
        2 => ":two:",
        3 => ":three:",
        _ => ":four:"
    };

    private static IReadOnlyList<string> GetCongratulationsMessages(
        ClassicStanding eventWinner)
    {
        return
        [
            $"\n\nВ последнем туре больше всех баллов набрала команда {eventWinner.EntryName} - {eventWinner.EventTotal}, это заслуживает обнимашек! :people_hugging:",
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
