namespace DiscordBot.PremierLeague;

public sealed class StandingsPublicationEligibilityService
{
    public bool IsUpdatedSinceYesterday(
        DateTimeOffset lastUpdatedUtc,
        DateTimeOffset utcNow)
    {
        return lastUpdatedUtc >= RigaTime.StartOfYesterdayUtc(utcNow);
    }
}
