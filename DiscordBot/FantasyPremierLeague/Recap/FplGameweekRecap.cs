using DiscordBot.FantasyPremierLeague.Historical;

namespace DiscordBot.FantasyPremierLeague.Recap;

public sealed record FplGameweekRecap(
    string Season,
    int EventId,
    int HighestScore,
    IReadOnlyList<FplManagerGameweekStatistics> HighestScorers,
    int LowestScore,
    IReadOnlyList<FplManagerGameweekStatistics> LowestScorers,
    decimal AverageScore,
    int BiggestClimb,
    IReadOnlyList<FplManagerGameweekStatistics> BiggestClimbers,
    int BiggestFall,
    IReadOnlyList<FplManagerGameweekStatistics> BiggestFallers,
    IReadOnlyList<FplManagerGameweekStatistics> NotableRankChanges,
    IReadOnlyList<FplManagerGameweekStatistics> Benchmasters,
    IReadOnlyList<FplCaptainPerformance> CaptainGeniuses);

public sealed record FplCaptainPerformance(
    FplManagerGameweekStatistics Manager,
    FplLineupPick Captain,
    int EffectivePoints);
