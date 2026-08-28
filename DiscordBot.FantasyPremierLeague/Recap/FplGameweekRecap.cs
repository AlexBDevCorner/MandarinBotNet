using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recognition;

namespace DiscordBot.FantasyPremierLeague.Recap;

public enum FplRecapHighlightKind
{
    GameweekWinner,
    BiggestClimb,
    BiggestFall,
    CaptainDisaster,
    TransferHitDisaster,
    BenchDisaster
}

public enum FplSeasonTrendKind
{
    FirstTimeAtTop,
    RecentWins,
    ConsecutiveRankRises,
    LeaderGapReduction
}

public sealed record FplGameweekRecap(
    string Season,
    int EventId,
    IReadOnlyList<FplRecapHighlight> Highlights,
    IReadOnlyList<FplSeasonTrend> SeasonTrends,
    IReadOnlyList<FplManagerGameweekStatistics> Standings,
    IReadOnlyList<FplAchievementAward> Achievements);

public abstract record FplRecapHighlight(FplRecapHighlightKind Kind);

public sealed record FplGameweekWinnerHighlight(
    int Score,
    IReadOnlyList<FplManagerGameweekStatistics> Winners)
    : FplRecapHighlight(FplRecapHighlightKind.GameweekWinner);

public sealed record FplRankMovementHighlight(
    FplRecapHighlightKind Kind,
    FplManagerGameweekStatistics Manager,
    int PreviousRank,
    int CurrentRank)
    : FplRecapHighlight(Kind);

public sealed record FplCaptainDisasterHighlight(
    FplManagerGameweekStatistics Manager,
    FplLineupPick Captain,
    FplLineupPick ViceCaptain)
    : FplRecapHighlight(FplRecapHighlightKind.CaptainDisaster);

public sealed record FplTransferHitHighlight(
    FplManagerGameweekStatistics Manager,
    int TransferCost)
    : FplRecapHighlight(FplRecapHighlightKind.TransferHitDisaster);

public sealed record FplBenchDisasterHighlight(
    FplManagerGameweekStatistics Manager)
    : FplRecapHighlight(FplRecapHighlightKind.BenchDisaster);

public abstract record FplSeasonTrend(FplSeasonTrendKind Kind, FplManagerGameweekStatistics Manager);

public sealed record FplFirstTimeAtTopTrend(FplManagerGameweekStatistics Leader)
    : FplSeasonTrend(FplSeasonTrendKind.FirstTimeAtTop, Leader)
{
    public bool SinceTrackingStarted { get; init; }
}

public sealed record FplRecentWinsTrend(
    FplManagerGameweekStatistics Manager,
    int WinCount,
    int Window)
    : FplSeasonTrend(FplSeasonTrendKind.RecentWins, Manager);

public sealed record FplConsecutiveRankRisesTrend(
    FplManagerGameweekStatistics Manager,
    int StreakLength)
    : FplSeasonTrend(FplSeasonTrendKind.ConsecutiveRankRises, Manager);

public sealed record FplLeaderGapReductionTrend(
    FplManagerGameweekStatistics Manager,
    int PreviousGap,
    int CurrentGap)
    : FplSeasonTrend(FplSeasonTrendKind.LeaderGapReduction, Manager);
