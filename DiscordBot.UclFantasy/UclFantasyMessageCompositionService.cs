using System.Globalization;
using DiscordBot.PremierLeague;

namespace DiscordBot.UclFantasy;

public sealed class UclFantasyMessageCompositionService(
    ConfiguredTimeZone configuredTimeZone)
{
    private static readonly CultureInfo RussianCulture = new("ru-RU");

    public string ComposeDeadlineReminder(
        DateTimeOffset deadlineUtc,
        DateTimeOffset utcNow)
    {
        var localDeadline = configuredTimeZone.FromUtc(deadlineUtc);
        var remaining = deadlineUtc - utcNow;
        var remainingMinutes = Math.Max(0, (int)remaining.TotalMinutes);
        var remainingHours = remainingMinutes / 60;
        var minutes = remainingMinutes % 60;

        return $"⚽ Дедлайн игрового дня ЛЧ: " +
            $"{localDeadline.ToString("dd MMMM yyyy, HH:mm", RussianCulture)} " +
            $"(Рига, Латвия). Осталось: " +
            $"{remainingHours.ToString(CultureInfo.InvariantCulture)} ч " +
            $"{minutes.ToString(CultureInfo.InvariantCulture)} мин. ⏳";
    }
}
