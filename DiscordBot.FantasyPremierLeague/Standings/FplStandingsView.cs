using DiscordBot.Responses;

namespace DiscordBot.FantasyPremierLeague.Standings;

public enum FplStandingsMovement
{
    None,
    Up,
    Down
}

public sealed record FplClassicStandingRow(
    int EntryId,
    string EntryName,
    string ManagerName,
    int Rank,
    int LastRank,
    int GameweekPoints,
    int TotalPoints,
    FplStandingsMovement Movement,
    int MovementDelta,
    int GapToLeader,
    bool IsAroundTarget)
{
    public bool HasGap => Rank != 1 && GapToLeader > 0;
}

public sealed record FplHeadToHeadStandingRow(
    string EntryName,
    int Rank,
    int LastRank,
    int TotalPoints,
    int MatchesPlayed,
    FplStandingsMovement Movement,
    int MovementDelta,
    int GapToLeader)
{
    public bool HasGap => Rank != 1 && GapToLeader > 0;
}

public sealed record FplClassicStandingsView(
    IReadOnlyList<FplClassicStandingRow> Rows,
    bool TargetMarked);

public sealed record FplHeadToHeadStandingsView(
    IReadOnlyList<FplHeadToHeadStandingRow> Rows);

public enum FplAroundLookupStatus
{
    Available,
    NotFound,
    Ambiguous
}

public sealed record FplAroundLookupResult(
    FplAroundLookupStatus Status,
    IReadOnlyList<FplClassicStandingRow> Rows,
    IReadOnlyList<FplClassicCandidate> Candidates);

public sealed record FplClassicCandidate(
    string EntryName,
    string ManagerName);
