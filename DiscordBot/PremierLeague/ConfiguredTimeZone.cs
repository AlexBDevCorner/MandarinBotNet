using DiscordBot.Jobs;

namespace DiscordBot.PremierLeague;

/// <summary>
/// Owns every conversion between UTC instants and the configured civil time.
/// Business rules should compare <see cref="DateTimeOffset"/> instants and use
/// this service only when a local calendar boundary or display is needed.
/// </summary>
public sealed class ConfiguredTimeZone(JobSchedulesOptions scheduleOptions)
{
    private readonly TimeZoneInfo _timeZone =
        JobSchedules.GetTimeZone(scheduleOptions);

    public DateTimeOffset FromUtc(DateTimeOffset utcInstant)
    {
        return TimeZoneInfo.ConvertTime(utcInstant, _timeZone);
    }

    public DateTimeOffset StartOfYesterdayUtc(DateTimeOffset utcNow)
    {
        var localYesterday = FromUtc(utcNow).Date.AddDays(-1);
        var unspecifiedLocalYesterday = DateTime.SpecifyKind(
            localYesterday,
            DateTimeKind.Unspecified);
        var utcYesterday = TimeZoneInfo.ConvertTimeToUtc(
            unspecifiedLocalYesterday,
            _timeZone);

        return new DateTimeOffset(utcYesterday, TimeSpan.Zero);
    }
}
