using System.Text.Json.Serialization;

namespace DiscordBot.UclFantasy;

/// <summary>
/// Tolerant transport models for the anonymously accessible UCL classic
/// league leaderboard endpoint:
/// <c>services/api/Leagues/{leagueId}/leagueleaderboard</c>.
/// The endpoint is observed, not documented, so every user-visible field has
/// several accepted JSON names and unknown payloads are rejected as invalid
/// rather than reported as a partial table. See
/// <c>docs/bot-configuration.md</c> for the operational assumptions.
/// </summary>
public sealed class UclFantasyLeagueStandingsResponse
{
    [JsonPropertyName("data")]
    public UclFantasyLeagueStandingsData? Data { get; init; }
}

public sealed class UclFantasyLeagueStandingsData
{
    [JsonPropertyName("value")]
    public UclFantasyLeagueStandingsValue? Value { get; init; }
}

public sealed class UclFantasyLeagueStandingsValue
{
    [JsonPropertyName("leagueId")]
    public long? LeagueId { get; init; }

    [JsonPropertyName("leagueID")]
    public long? LeagueIdUpper { get; init; }

    [JsonPropertyName("id")]
    public long? Id { get; init; }

    [JsonPropertyName("leagueName")]
    public string? LeagueName { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("phaseId")]
    public int? PhaseId { get; init; }

    [JsonPropertyName("matchdayId")]
    public int? MatchdayId { get; init; }

    [JsonPropertyName("mdId")]
    public int? MatchdayIdShort { get; init; }

    [JsonPropertyName("totalEntries")]
    public int? TotalEntries { get; init; }

    [JsonPropertyName("totalMembers")]
    public int? TotalMembers { get; init; }

    [JsonPropertyName("total")]
    public int? Total { get; init; }

    [JsonPropertyName("totalCount")]
    public int? TotalCount { get; init; }

    [JsonPropertyName("count")]
    public int? Count { get; init; }

    [JsonPropertyName("pageNo")]
    public int? PageNo { get; init; }

    [JsonPropertyName("vPageNo")]
    public int? PageNoVersioned { get; init; }

    [JsonPropertyName("page")]
    public int? Page { get; init; }

    [JsonPropertyName("pageSize")]
    public int? PageSize { get; init; }

    [JsonPropertyName("vPageChunk")]
    public int? PageChunkVersioned { get; init; }

    [JsonPropertyName("pageChunk")]
    public int? PageChunk { get; init; }

    [JsonPropertyName("vPageOneChunk")]
    public int? PageOneChunkVersioned { get; init; }

    [JsonPropertyName("leaderboard")]
    public List<UclFantasyLeagueStandingsMemberPayload>? Leaderboard { get; init; }

    [JsonPropertyName("entries")]
    public List<UclFantasyLeagueStandingsMemberPayload>? Entries { get; init; }

    [JsonPropertyName("results")]
    public List<UclFantasyLeagueStandingsMemberPayload>? Results { get; init; }

    [JsonPropertyName("standings")]
    public List<UclFantasyLeagueStandingsMemberPayload>? Standings { get; init; }

    [JsonPropertyName("members")]
    public List<UclFantasyLeagueStandingsMemberPayload>? Members { get; init; }

    [JsonPropertyName("teams")]
    public List<UclFantasyLeagueStandingsMemberPayload>? Teams { get; init; }

    [JsonPropertyName("rows")]
    public List<UclFantasyLeagueStandingsMemberPayload>? Rows { get; init; }

    public long ResolvedLeagueId =>
        LeagueId ?? LeagueIdUpper ?? Id ?? 0;

    public string? ResolvedLeagueName =>
        LeagueName ?? Name;

    public int? ResolvedTotalMembers =>
        TotalEntries ?? TotalMembers ?? TotalCount ?? Total ?? Count;

    public IReadOnlyList<UclFantasyLeagueStandingsMemberPayload> ResolvedMembers()
    {
        if (Leaderboard is { Count: > 0 })
        {
            return Leaderboard;
        }

        if (Entries is { Count: > 0 })
        {
            return Entries;
        }

        if (Results is { Count: > 0 })
        {
            return Results;
        }

        if (Standings is { Count: > 0 })
        {
            return Standings;
        }

        if (Members is { Count: > 0 })
        {
            return Members;
        }

        if (Teams is { Count: > 0 })
        {
            return Teams;
        }

        if (Rows is { Count: > 0 })
        {
            return Rows;
        }

        return [];
    }
}

public sealed class UclFantasyLeagueStandingsMemberPayload
{
    [JsonPropertyName("entryId")]
    public long? EntryId { get; init; }

    [JsonPropertyName("entry")]
    public long? Entry { get; init; }

    [JsonPropertyName("teamId")]
    public long? TeamId { get; init; }

    [JsonPropertyName("id")]
    public long? Id { get; init; }

    [JsonPropertyName("userId")]
    public long? UserId { get; init; }

    [JsonPropertyName("teamName")]
    public string? TeamName { get; init; }

    [JsonPropertyName("entryName")]
    public string? EntryName { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("managerName")]
    public string? ManagerName { get; init; }

    [JsonPropertyName("playerName")]
    public string? PlayerName { get; init; }

    [JsonPropertyName("userName")]
    public string? UserName { get; init; }

    [JsonPropertyName("coachName")]
    public string? CoachName { get; init; }

    [JsonPropertyName("rank")]
    public int? Rank { get; init; }

    [JsonPropertyName("position")]
    public int? Position { get; init; }

    [JsonPropertyName("pos")]
    public int? PositionShort { get; init; }

    [JsonPropertyName("standing")]
    public int? Standing { get; init; }

    [JsonPropertyName("lastRank")]
    public int? LastRank { get; init; }

    [JsonPropertyName("previousRank")]
    public int? PreviousRank { get; init; }

    [JsonPropertyName("prevRank")]
    public int? PreviousRankShort { get; init; }

    [JsonPropertyName("last_rank")]
    public int? LastRankSnake { get; init; }

    [JsonPropertyName("total")]
    public int? Total { get; init; }

    [JsonPropertyName("totalPoints")]
    public int? TotalPoints { get; init; }

    [JsonPropertyName("points")]
    public int? Points { get; init; }

    [JsonPropertyName("overallPoints")]
    public int? OverallPoints { get; init; }

    [JsonPropertyName("eventTotal")]
    public int? EventTotal { get; init; }

    [JsonPropertyName("matchdayPoints")]
    public int? MatchdayPoints { get; init; }

    [JsonPropertyName("gwPoints")]
    public int? GameweekPoints { get; init; }

    [JsonPropertyName("roundPoints")]
    public int? RoundPoints { get; init; }

    [JsonPropertyName("weekPoints")]
    public int? WeekPoints { get; init; }

    [JsonPropertyName("lastMatchdayPoints")]
    public int? LastMatchdayPoints { get; init; }
}
