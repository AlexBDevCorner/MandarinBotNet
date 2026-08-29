namespace DiscordBot.BenchWarming;

public class BenchWarmingQueryService(IBenchWarmingLeagueStore store)
{
    public virtual BenchWarmingSeasonOverview? GetSeasonOverview()
    {
        var season = store.GetLatestSeason();
        if (season is null)
        {
            return null;
        }

        var tracking = store.GetTrackingInfo(season);
        if (tracking is null)
        {
            return null;
        }

        var roundStandings = store.GetSeasonRoundStandings(season);

        var latestEventId = tracking.LatestEventId;
        var latestRoundStandings = roundStandings
            .Where(standing => standing.EventId == latestEventId)
            .OrderByDescending(standing => standing.Points)
            .ThenBy(standing => standing.EntryName, StringComparer.OrdinalIgnoreCase)
            .Select(standing => new BenchWarmingEntryStanding(
                standing.EntryId,
                standing.EntryName,
                standing.Points))
            .ToArray();

        var seasonStandings = roundStandings
            .GroupBy(standing => standing.EntryId)
            .Select(group => new BenchWarmingEntryStanding(
                group.Key,
                group.First().EntryName,
                group.Sum(standing => standing.Points)))
            .OrderByDescending(standing => standing.Points)
            .ThenBy(standing => standing.EntryName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var records = BuildSeasonRecords(roundStandings);

        return new BenchWarmingSeasonOverview(
            season,
            tracking,
            seasonStandings,
            latestEventId,
            latestRoundStandings,
            records);
    }

    public virtual BenchWarmingRoundSummaryResult GetRound(int eventId)
    {
        var season = store.GetLatestSeason();
        if (season is null)
        {
            return BenchWarmingRoundSummaryResult.ForNoData();
        }

        var tracking = store.GetTrackingInfo(season);
        if (tracking is null)
        {
            return BenchWarmingRoundSummaryResult.ForNoData();
        }

        if (eventId < tracking.FirstEventId || eventId > tracking.LatestEventId)
        {
            return BenchWarmingRoundSummaryResult.ForNotTracked(tracking);
        }

        var roundStandings = store.GetSeasonRoundStandings(season);
        var isTracked = roundStandings.Any(standing => standing.EventId == eventId);
        if (!isTracked)
        {
            return BenchWarmingRoundSummaryResult.ForNotTracked(tracking);
        }

        var standings = roundStandings
            .Where(standing => standing.EventId == eventId)
            .OrderByDescending(standing => standing.Points)
            .ThenBy(standing => standing.EntryName, StringComparer.OrdinalIgnoreCase)
            .Select(standing => new BenchWarmingEntryStanding(
                standing.EntryId,
                standing.EntryName,
                standing.Points))
            .ToArray();

        var topBenchPlayer = store.GetRoundBenchPoints(season, eventId)
            .Where(player => player.Points > 0)
            .OrderByDescending(player => player.Points)
            .ThenBy(player => player.PlayerWebName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        var summary = new BenchWarmingRoundSummary(
            season,
            eventId,
            standings,
            topBenchPlayer);

        return BenchWarmingRoundSummaryResult.ForAvailable(summary);
    }

    public virtual BenchWarmingTeamLookupResult GetTeam(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var season = store.GetLatestSeason();
        if (season is null)
        {
            return BenchWarmingTeamLookupResult.ForNoData();
        }

        var roundStandings = store.GetSeasonRoundStandings(season);
        if (roundStandings.Count == 0)
        {
            return BenchWarmingTeamLookupResult.ForNoData();
        }

        var resolution = ResolveTeam(roundStandings, query.Trim());
        if (resolution.Outcome == BenchWarmingTeamLookupOutcome.NotFound)
        {
            return BenchWarmingTeamLookupResult.ForNotFound();
        }

        if (resolution.Outcome == BenchWarmingTeamLookupOutcome.Ambiguous)
        {
            return BenchWarmingTeamLookupResult.ForAmbiguous(resolution.Candidates!);
        }

        var entryId = resolution.EntryId!.Value;
        var tracking = store.GetTrackingInfo(season);
        var trackingStartedEventId = tracking?.FirstEventId
            ?? roundStandings.Min(standing => standing.EventId);

        var teamRounds = roundStandings
            .Where(standing => standing.EntryId == entryId)
            .OrderBy(standing => standing.EventId)
            .ToArray();

        var totalPoints = teamRounds.Sum(standing => standing.Points);
        var averagePoints = teamRounds.Length == 0
            ? 0d
            : totalPoints / (double)teamRounds.Length;

        var record = teamRounds
            .OrderByDescending(standing => standing.Points)
            .ThenBy(standing => standing.EventId)
            .First();
        var highestRoundPoints = record.Points;
        var highestRoundEventId = record.EventId;

        var longestEightPlusStreak = FindLongestStreak(
                teamRounds,
                points => points >= 8)?
            .Length ?? 0;
        var longestCleanBenchStreak = FindLongestStreak(
                teamRounds,
                points => points == 0)?
            .Length ?? 0;

        var profile = new BenchWarmingTeamProfile(
            season,
            teamRounds[0].EntryId,
            teamRounds[0].EntryName,
            trackingStartedEventId,
            totalPoints,
            averagePoints,
            highestRoundPoints,
            highestRoundEventId,
            longestEightPlusStreak,
            longestCleanBenchStreak,
            teamRounds);

        return BenchWarmingTeamLookupResult.ForAvailable(profile);
    }

    private static BenchWarmingSeasonRecords BuildSeasonRecords(
        IReadOnlyList<BenchWarmingEntryRoundStanding> roundStandings)
    {
        var biggestBenchDisaster = roundStandings
            .OrderByDescending(standing => standing.Points)
            .ThenBy(standing => standing.EventId)
            .ThenBy(standing => standing.EntryName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        var average = roundStandings.Count == 0
            ? 0d
            : roundStandings.Sum(standing => standing.Points)
                / (double)roundStandings.Count;

        var longestEightPlusStreak = FindLongestStreak(
            roundStandings,
            points => points >= 8);
        var longestCleanBenchStreak = FindLongestStreak(
            roundStandings,
            points => points == 0);

        return new BenchWarmingSeasonRecords(
            biggestBenchDisaster,
            average,
            longestEightPlusStreak,
            longestCleanBenchStreak);
    }

    private static (
        BenchWarmingTeamLookupOutcome Outcome,
        int? EntryId,
        IReadOnlyList<string>? Candidates) ResolveTeam(
        IReadOnlyList<BenchWarmingEntryRoundStanding> roundStandings,
        string query)
    {
        var entries = roundStandings
            .GroupBy(standing => standing.EntryId)
            .Select(group => new
            {
                EntryId = group.Key,
                EntryName = group
                    .OrderByDescending(standing => standing.EventId)
                    .First()
                    .EntryName
            })
            .ToArray();

        var exact = entries.FirstOrDefault(entry =>
            string.Equals(entry.EntryName, query, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return (BenchWarmingTeamLookupOutcome.Available, exact.EntryId, null);
        }

        var partial = entries
            .Where(entry => entry.EntryName.Contains(
                query,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (partial.Length == 0)
        {
            return (BenchWarmingTeamLookupOutcome.NotFound, null, null);
        }

        if (partial.Length > 1)
        {
            var candidates = partial
                .Select(entry => entry.EntryName)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return (BenchWarmingTeamLookupOutcome.Ambiguous, null, candidates);
        }

        return (BenchWarmingTeamLookupOutcome.Available, partial[0].EntryId, null);
    }

    private static BenchWarmingStreakRecord? FindLongestStreak(
        IEnumerable<BenchWarmingEntryRoundStanding> rounds,
        Func<int, bool> qualifies)
    {
        BenchWarmingStreakRecord? best = null;

        foreach (var group in rounds.GroupBy(standing => standing.EntryId))
        {
            var ordered = group
                .OrderBy(standing => standing.EventId)
                .ToArray();

            var index = 0;
            while (index < ordered.Length)
            {
                var start = ordered[index];
                if (!qualifies(start.Points))
                {
                    index++;
                    continue;
                }

                var endIndex = index;
                while (endIndex + 1 < ordered.Length &&
                       ordered[endIndex + 1].EventId == ordered[endIndex].EventId + 1 &&
                       qualifies(ordered[endIndex + 1].Points))
                {
                    endIndex++;
                }

                var length = endIndex - index + 1;
                var record = new BenchWarmingStreakRecord(
                    start.EntryId,
                    start.EntryName,
                    length,
                    start.EventId,
                    ordered[endIndex].EventId);
                best = PickBetterStreak(best, record);

                index = endIndex + 1;
            }
        }

        return best;
    }

    private static BenchWarmingStreakRecord? PickBetterStreak(
        BenchWarmingStreakRecord? current,
        BenchWarmingStreakRecord candidate)
    {
        if (current is null)
        {
            return candidate;
        }

        if (candidate.Length != current.Length)
        {
            return candidate.Length > current.Length ? candidate : current;
        }

        if (candidate.EndEventId != current.EndEventId)
        {
            return candidate.EndEventId < current.EndEventId ? candidate : current;
        }

        return string.Compare(
                candidate.EntryName,
                current.EntryName,
                StringComparison.OrdinalIgnoreCase) < 0
            ? candidate
            : current;
    }
}
