using TimeZoneConverter;

namespace DiscordBot.PremierLeague;

/// <summary>
/// Owns every conversion between UTC instants and Europe/Riga civil time.
/// Business rules should compare <see cref="DateTimeOffset"/> instants and use
/// this class only when a Riga calendar boundary or localized display is needed.
/// </summary>
public static class RigaTime
{
    private static readonly TimeZoneInfo TimeZone =
        TZConvert.GetTimeZoneInfo("Europe/Riga");

    public static DateTimeOffset FromUtc(DateTimeOffset utcInstant)
    {
        return TimeZoneInfo.ConvertTime(utcInstant, TimeZone);
    }

    public static DateTimeOffset StartOfYesterdayUtc(DateTimeOffset utcNow)
    {
        var localYesterday = FromUtc(utcNow).Date.AddDays(-1);
        var unspecifiedLocalYesterday = DateTime.SpecifyKind(
            localYesterday,
            DateTimeKind.Unspecified);
        var utcYesterday = TimeZoneInfo.ConvertTimeToUtc(
            unspecifiedLocalYesterday,
            TimeZone);

        return new DateTimeOffset(utcYesterday, TimeSpan.Zero);
    }
}
