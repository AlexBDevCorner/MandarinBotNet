namespace DiscordBot.BenchWarming;

public sealed record BenchWarmingSeasonOverview(
    string Season,
    BenchWarmingTrackingInfo Tracking,
    IReadOnlyList<BenchWarmingEntryStanding> SeasonStandings,
    int LatestEventId,
    IReadOnlyList<BenchWarmingEntryStanding> LatestRoundStandings,
    BenchWarmingSeasonRecords Records);

public sealed record BenchWarmingSeasonRecords(
    BenchWarmingEntryRoundStanding? BiggestBenchDisaster,
    double AveragePointsPerManagerRound,
    BenchWarmingStreakRecord? LongestEightPlusStreak,
    BenchWarmingStreakRecord? LongestCleanBenchStreak);

public sealed record BenchWarmingStreakRecord(
    int EntryId,
    string EntryName,
    int Length,
    int StartEventId,
    int EndEventId);

public sealed record BenchWarmingTeamCandidate(
    int EntryId,
    string EntryName);

public sealed record BenchWarmingTeamProfile(
    string Season,
    int EntryId,
    string EntryName,
    int FirstTrackedEventId,
    int TotalPoints,
    double AveragePoints,
    int HighestRoundPoints,
    int HighestRoundEventId,
    int LongestEightPlusStreak,
    int LongestCleanBenchStreak,
    IReadOnlyList<BenchWarmingEntryRoundStanding> History);

public enum BenchWarmingTeamLookupOutcome
{
    Available,
    NoData,
    NotFound,
    Ambiguous
}

public sealed record BenchWarmingTeamLookupResult(
    BenchWarmingTeamLookupOutcome Outcome,
    BenchWarmingTeamProfile? Profile = null,
    IReadOnlyList<BenchWarmingTeamCandidate>? Candidates = null)
{
    public static BenchWarmingTeamLookupResult ForNoData()
        => new(BenchWarmingTeamLookupOutcome.NoData);

    public static BenchWarmingTeamLookupResult ForNotFound()
        => new(BenchWarmingTeamLookupOutcome.NotFound);

    public static BenchWarmingTeamLookupResult ForAmbiguous(
        IReadOnlyList<BenchWarmingTeamCandidate> candidates)
        => new(BenchWarmingTeamLookupOutcome.Ambiguous, Candidates: candidates);

    public static BenchWarmingTeamLookupResult ForAvailable(
        BenchWarmingTeamProfile profile)
        => new(BenchWarmingTeamLookupOutcome.Available, Profile: profile);
}

public enum BenchWarmingRoundSummaryOutcome
{
    Available,
    NoData,
    NotTracked
}

public sealed record BenchWarmingRoundSummary(
    string Season,
    int EventId,
    IReadOnlyList<BenchWarmingEntryStanding> Standings,
    BenchWarmingPlayerPoints? TopBenchPlayer);

public sealed record BenchWarmingRoundSummaryResult(
    BenchWarmingRoundSummaryOutcome Outcome,
    BenchWarmingRoundSummary? Summary = null,
    BenchWarmingTrackingInfo? Tracking = null)
{
    public static BenchWarmingRoundSummaryResult ForNoData()
        => new(BenchWarmingRoundSummaryOutcome.NoData);

    public static BenchWarmingRoundSummaryResult ForNotTracked(
        BenchWarmingTrackingInfo tracking)
        => new(BenchWarmingRoundSummaryOutcome.NotTracked, Tracking: tracking);

    public static BenchWarmingRoundSummaryResult ForAvailable(
        BenchWarmingRoundSummary summary)
        => new(BenchWarmingRoundSummaryOutcome.Available, Summary: summary);
}
