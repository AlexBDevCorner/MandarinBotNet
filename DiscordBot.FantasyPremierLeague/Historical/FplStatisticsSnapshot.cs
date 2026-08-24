namespace DiscordBot.FantasyPremierLeague.Historical;

public sealed record FplGameweekSnapshot(
    string Season,
    int EventId,
    DateTimeOffset DeadlineUtc,
    DateTimeOffset StandingsUpdatedAtUtc,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<FplManagerGameweekStatistics> Managers);

public sealed record FplManagerGameweekStatistics(
    int EntryId,
    string EntryName,
    string ManagerName,
    int EventScore,
    int TotalScore,
    int Rank,
    int LastRank,
    int RankChange,
    int BenchPoints,
    IReadOnlyList<FplLineupPick> Lineup)
{
    public int TransferCost { get; init; }
}

public sealed record FplLineupPick(
    int PlayerId,
    string PlayerName,
    int Position,
    int Multiplier,
    bool IsCaptain,
    bool IsViceCaptain,
    int Points)
{
    public bool IsBench => Multiplier == 0;
}
