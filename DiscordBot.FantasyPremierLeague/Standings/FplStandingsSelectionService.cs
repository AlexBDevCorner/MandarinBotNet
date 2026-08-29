using System.Globalization;
using DiscordBot.Responses;

namespace DiscordBot.FantasyPremierLeague.Standings;

public sealed class FplStandingsSelectionService
{
    private const int AroundWindowSize = 5;
    private const int AroundSideCount = 2;
    private const int MaxCandidateCount = 10;

    public FplClassicStandingsView SelectClassic(
        IReadOnlyCollection<ClassicStanding> standings,
        int? top)
    {
        ArgumentNullException.ThrowIfNull(standings);

        var ordered = OrderClassic(standings);
        var rows = ApplyClassicRows(ordered);
        if (top is > 0)
        {
            rows = rows.Take(top.Value).ToList();
        }

        return new FplClassicStandingsView(rows, TargetMarked: false);
    }

    public FplAroundLookupResult ResolveAround(
        IReadOnlyCollection<ClassicStanding> standings,
        string query)
    {
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var ordered = OrderClassic(standings);
        var trimmed = query.Trim();

        var matched = ResolveTarget(ordered, trimmed);
        if (matched.Status != FplAroundLookupStatus.Available)
        {
            return matched;
        }

        var target = matched.Rows.Single();
        var targetIndex = ordered.FindIndex(entry => entry.Entry == target.EntryId);
        var window = BuildWindow(ordered, targetIndex, target.EntryId);

        return new FplAroundLookupResult(
            FplAroundLookupStatus.Available,
            window,
            []);
    }

    public FplHeadToHeadStandingsView SelectHeadToHead(
        IReadOnlyCollection<HeadToHeadStanding> standings,
        int? top)
    {
        ArgumentNullException.ThrowIfNull(standings);

        var ordered = standings
            .OrderBy(entry => entry.Rank)
            .ThenBy(entry => entry.EntryName, StringComparer.Ordinal)
            .ToList();
        var leaderTotal = ordered.Count > 0 ? ordered[0].Total : 0;

        var rows = ordered.Select(entry => BuildHeadToHeadRow(entry, leaderTotal))
            .ToList();
        if (top is > 0)
        {
            rows = rows.Take(top.Value).ToList();
        }

        return new FplHeadToHeadStandingsView(rows);
    }

    private static List<ClassicStanding> OrderClassic(
        IReadOnlyCollection<ClassicStanding> standings)
    {
        return standings
            .OrderBy(entry => entry.Rank)
            .ThenBy(entry => entry.Entry)
            .ToList();
    }

    private static List<FplClassicStandingRow> ApplyClassicRows(
        IReadOnlyList<ClassicStanding> ordered)
    {
        var leaderTotal = ordered.Count > 0 ? ordered[0].Total : 0;
        return ordered.Select(entry => BuildClassicRow(entry, leaderTotal))
            .ToList();
    }

    private static FplClassicStandingRow BuildClassicRow(
        ClassicStanding entry,
        int leaderTotal)
    {
        var (movement, delta) = CalculateMovement(entry.Rank, entry.LastRank);
        return new FplClassicStandingRow(
            entry.Entry,
            entry.EntryName,
            entry.PlayerName,
            entry.Rank,
            entry.LastRank,
            entry.EventTotal,
            entry.Total,
            movement,
            delta,
            leaderTotal - entry.Total,
            IsAroundTarget: false);
    }

    private static FplHeadToHeadStandingRow BuildHeadToHeadRow(
        HeadToHeadStanding entry,
        int leaderTotal)
    {
        var (movement, delta) = CalculateMovement(entry.Rank, entry.LastRank);
        return new FplHeadToHeadStandingRow(
            entry.EntryName,
            entry.Rank,
            entry.LastRank,
            entry.Total,
            entry.MatchesPlayed,
            movement,
            delta,
            leaderTotal - entry.Total);
    }

    private static (FplStandingsMovement Movement, int Delta) CalculateMovement(
        int rank,
        int lastRank)
    {
        if (lastRank <= 0)
        {
            return (FplStandingsMovement.None, 0);
        }

        if (rank < lastRank)
        {
            return (FplStandingsMovement.Up, lastRank - rank);
        }

        if (rank > lastRank)
        {
            return (FplStandingsMovement.Down, rank - lastRank);
        }

        return (FplStandingsMovement.None, 0);
    }

    private static FplAroundLookupResult ResolveTarget(
        IReadOnlyList<ClassicStanding> ordered,
        string query)
    {
        var lower = query.ToLower(CultureInfo.InvariantCulture);

        ClassicStanding? exact = ordered.FirstOrDefault(entry =>
            string.Equals(
                entry.EntryName.Trim(),
                query,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                entry.PlayerName.Trim(),
                query,
                StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
        {
            return SingleTarget(exact, ordered);
        }

        var partial = ordered.Where(entry =>
                entry.EntryName.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                entry.PlayerName.Contains(lower, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (partial.Count == 0)
        {
            return new FplAroundLookupResult(
                FplAroundLookupStatus.NotFound,
                [],
                []);
        }

        if (partial.Count == 1)
        {
            return SingleTarget(partial[0], ordered);
        }

        var candidates = partial
            .Take(MaxCandidateCount)
            .Select(entry => new FplClassicCandidate(
                entry.EntryName,
                entry.PlayerName))
            .ToList();

        return new FplAroundLookupResult(
            FplAroundLookupStatus.Ambiguous,
            [],
            candidates);
    }

    private static FplAroundLookupResult SingleTarget(
        ClassicStanding target,
        IReadOnlyList<ClassicStanding> ordered)
    {
        var leaderTotal = ordered.Count > 0 ? ordered[0].Total : 0;
        var row = BuildClassicRow(target, leaderTotal) with { IsAroundTarget = true };
        return new FplAroundLookupResult(
            FplAroundLookupStatus.Available,
            [row],
            []);
    }

    private static IReadOnlyList<FplClassicStandingRow> BuildWindow(
        IReadOnlyList<ClassicStanding> ordered,
        int targetIndex,
        int targetEntryId)
    {
        var leaderTotal = ordered.Count > 0 ? ordered[0].Total : 0;
        var start = Math.Max(0, targetIndex - AroundSideCount);
        var end = Math.Min(ordered.Count - 1, targetIndex + AroundSideCount);

        if (end - start + 1 > AroundWindowSize)
        {
            end = start + AroundWindowSize - 1;
        }

        if (end - start + 1 < AroundWindowSize)
        {
            if (start == 0)
            {
                end = Math.Min(ordered.Count - 1, start + AroundWindowSize - 1);
            }
            else if (end == ordered.Count - 1)
            {
                start = Math.Max(0, end - AroundWindowSize + 1);
            }
        }

        var rows = new List<FplClassicStandingRow>();
        for (var index = start; index <= end; index++)
        {
            var entry = ordered[index];
            var row = BuildClassicRow(entry, leaderTotal);
            if (entry.Entry == targetEntryId)
            {
                row = row with { IsAroundTarget = true };
            }

            rows.Add(row);
        }

        return rows;
    }
}
