namespace DiscordBot.PremierLeague;

public sealed class StandingsPublicationEligibilityService(
    ConfiguredTimeZone configuredTimeZone)
{
    public bool IsUpdatedSinceYesterday(
        DateTimeOffset lastUpdatedUtc,
        DateTimeOffset utcNow)
    {
        return lastUpdatedUtc >= configuredTimeZone.StartOfYesterdayUtc(utcNow);
    }
}
