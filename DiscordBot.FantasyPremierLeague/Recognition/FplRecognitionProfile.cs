namespace DiscordBot.FantasyPremierLeague.Recognition;

public sealed record FplAchievementCount(
    string AchievementKey,
    string AchievementName,
    int Count);

public sealed record FplManagerReference(
    int EntryId,
    string EntryName,
    string ManagerName);

public sealed record FplManagerProfile(
    string Season,
    int TrackingStartedEventId,
    int EntryId,
    string EntryName,
    string ManagerName,
    int CurrentRank,
    int TotalScore,
    int GameweekWins,
    int BestGameweekScore,
    int BestGameweekEventId,
    int HighestBenchPoints,
    int HighestBenchPointsEventId,
    IReadOnlyList<FplAchievementCount> AchievementCounts)
{
    public int TotalAchievements => AchievementCounts.Sum(count => count.Count);
}

public enum FplManagerProfileLookupOutcome
{
    Available,
    NoData,
    NotFound,
    Ambiguous
}

public sealed record FplManagerProfileLookupResult(
    FplManagerProfileLookupOutcome Outcome,
    FplManagerProfile? Profile = null,
    IReadOnlyList<FplManagerReference>? Candidates = null)
{
    public static FplManagerProfileLookupResult ForNoData()
        => new(FplManagerProfileLookupOutcome.NoData);

    public static FplManagerProfileLookupResult ForNotFound()
        => new(FplManagerProfileLookupOutcome.NotFound);

    public static FplManagerProfileLookupResult ForAmbiguous(
        IReadOnlyList<FplManagerReference> candidates)
        => new(FplManagerProfileLookupOutcome.Ambiguous, Candidates: candidates);

    public static FplManagerProfileLookupResult ForAvailable(
        FplManagerProfile profile)
        => new(FplManagerProfileLookupOutcome.Available, Profile: profile);
}

public sealed record FplAchievementTotalRankingEntry(
    int EntryId,
    string EntryName,
    string ManagerName,
    int CurrentRank,
    int TotalAwards);

public sealed record FplAchievementCategoryRanking(
    string AchievementKey,
    int Count,
    IReadOnlyList<FplManagerReference> Leaders);

public sealed record FplAchievementSeasonSummary(
    string Season,
    int TrackingStartedEventId,
    IReadOnlyList<FplAchievementTotalRankingEntry> TotalAwardRanking,
    IReadOnlyList<FplAchievementCategoryRanking> PerAchievementLeaders,
    int GameweekWinTopCount,
    IReadOnlyList<FplManagerReference> GameweekWinLeaders);
