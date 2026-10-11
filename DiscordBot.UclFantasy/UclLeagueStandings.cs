namespace DiscordBot.UclFantasy;

/// <summary>
/// Normalized, presentation-ready UCL league standings. Transport models stay
/// in <c>UclFantasyStandingsResponses</c>; this model is what commands, the
/// snapshot fingerprint and the scheduled notifier compare and render.
/// </summary>
public sealed record UclLeagueStanding(
    long StableId,
    string TeamName,
    string ManagerName,
    int Rank,
    int LastRank,
    int TotalPoints,
    int MatchdayPoints)
{
    public bool HasMovement => LastRank > 0 && LastRank != Rank;

    public int MovementDelta => HasMovement ? Math.Abs(LastRank - Rank) : 0;

    public bool MovedUp => HasMovement && Rank < LastRank;
}

public sealed record UclLeagueStandings(
    int LeagueId,
    string LeagueName,
    int PhaseId,
    int MatchdayId,
    IReadOnlyList<UclLeagueStanding> Members)
{
    public int TotalMembers => Members.Count;
}

/// <summary>
/// Read-only standings request. <c>MatchdayId</c> must already be resolved
/// (pinned or auto-detected from the fixtures feed); the client never
/// guesses the current matchday itself.
/// </summary>
public sealed record UclLeagueStandingsQuery(
    int LeagueId,
    int OptType,
    int PhaseId,
    int MatchdayId,
    int PageSize,
    int MaxPages);
