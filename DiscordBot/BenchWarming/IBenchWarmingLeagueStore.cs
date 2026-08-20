namespace DiscordBot.BenchWarming;

public sealed record BenchWarmingPlayerPoints(
    int EntryId,
    string EntryName,
    int PlayerId,
    string PlayerWebName,
    int Points);

public sealed record BenchWarmingEntryStanding(
    int EntryId,
    string EntryName,
    int Points);

public sealed record BenchWarmingRoundResult(
    string Season,
    int EventId,
    bool WasAlreadyCalculated,
    IReadOnlyList<BenchWarmingPlayerPoints> BenchPoints,
    IReadOnlyList<BenchWarmingEntryStanding> RoundStandings,
    IReadOnlyList<BenchWarmingEntryStanding> SeasonStandings);

public interface IBenchWarmingLeagueStore
{
    bool IsRoundCalculated(string season, int eventId);

    void SaveRound(
        string season,
        int eventId,
        IReadOnlyList<BenchWarmingPlayerPoints> benchPoints,
        DateTimeOffset calculatedAtUtc);

    IReadOnlyList<BenchWarmingEntryStanding> GetSeasonStandings(string season);

    string? GetLatestSeason();
}
