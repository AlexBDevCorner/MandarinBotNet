using DiscordBot.PremierLeague;

namespace DiscordBot.UclFantasy;

public sealed class UclFantasyMessageCompositionService(
    ConfiguredTimeZone configuredTimeZone)
{
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
}
