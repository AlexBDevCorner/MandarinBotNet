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

        var latestEntryNames = GetLatestEntryNames(roundStandings);

        var latestEventId = tracking.LatestEventId;
        var latestRoundStandings = roundStandings
            .Where(standing => standing.EventId == latestEventId)
            .OrderByDescending(standing => standing.Points)
            .ThenBy(standing => standing.EntryName, StringComparer.OrdinalIgnoreCase)
            .Select(standing => new BenchWarmingEntryStanding(
                standing.EntryId,
                latestEntryNames.GetValueOrDefault(standing.EntryId, standing.EntryName),
                standing.Points))
            .ToArray();

        var seasonStandings = roundStandings
            .GroupBy(standing => standing.EntryId)
            .Select(group => new BenchWarmingEntryStanding(
                group.Key,
                latestEntryNames.GetValueOrDefault(group.Key, group.First().EntryName),
                group.Sum(standing => standing.Points)))
            .OrderByDescending(standing => standing.Points)
            .ThenBy(standing => standing.EntryName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var records = BuildSeasonRecords(roundStandings, latestEntryNames);

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

        if (!store.IsRoundCalculated(season, eventId))
        {
            return BenchWarmingRoundSummaryResult.ForNotTracked(tracking);
        }

        var roundStandings = store.GetSeasonRoundStandings(season);
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

        var teamLatestNames = new Dictionary<int, string>
        {
            [entryId] = teamRounds[^1].EntryName
        };

        var longestEightPlusStreak = FindLongestStreak(
                teamRounds,
                teamLatestNames,
                round => round.Points >= 8)?
            .Length ?? 0;
        var longestCleanBenchStreak = FindLongestStreak(
                teamRounds,
                teamLatestNames,
                round => round.Points == 0 && !IsBenchBoost(round.ActiveChip))?
            .Length ?? 0;

        var profile = new BenchWarmingTeamProfile(
            season,
            teamRounds[0].EntryId,
            teamRounds[^1].EntryName,
            teamRounds[0].EventId,
            totalPoints,
            averagePoints,
            highestRoundPoints,
            highestRoundEventId,
            longestEightPlusStreak,
            longestCleanBenchStreak,
            teamRounds);

        return BenchWarmingTeamLookupResult.ForAvailable(profile);
    }

    private static IReadOnlyDictionary<int, string> GetLatestEntryNames(
        IReadOnlyList<BenchWarmingEntryRoundStanding> roundStandings)
    {
        return roundStandings
            .GroupBy(standing => standing.EntryId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(standing => standing.EventId)
                    .First()
                    .EntryName);
    }

    private static BenchWarmingSeasonRecords BuildSeasonRecords(
        IReadOnlyList<BenchWarmingEntryRoundStanding> roundStandings,
        IReadOnlyDictionary<int, string> latestEntryNames)
    {
        var biggestBenchDisaster = roundStandings
            .OrderByDescending(standing => standing.Points)
            .ThenBy(standing => standing.EventId)
            .ThenBy(standing => standing.EntryName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (biggestBenchDisaster is not null)
        {
            biggestBenchDisaster = biggestBenchDisaster with
            {
                EntryName = latestEntryNames.GetValueOrDefault(
                    biggestBenchDisaster.EntryId,
                    biggestBenchDisaster.EntryName)
            };
        }

        var average = roundStandings.Count == 0
            ? 0d
            : roundStandings.Sum(standing => standing.Points)
                / (double)roundStandings.Count;

        var longestEightPlusStreak = FindLongestStreak(
            roundStandings,
            latestEntryNames,
            round => round.Points >= 8);
        var longestCleanBenchStreak = FindLongestStreak(
            roundStandings,
            latestEntryNames,
            round => round.Points == 0 && !IsBenchBoost(round.ActiveChip));

        return new BenchWarmingSeasonRecords(
            biggestBenchDisaster,
            average,
            longestEightPlusStreak,
            longestCleanBenchStreak);
    }

    private static (
        BenchWarmingTeamLookupOutcome Outcome,
        int? EntryId,
        IReadOnlyList<BenchWarmingTeamCandidate>? Candidates) ResolveTeam(
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

        if (int.TryParse(query, out var numericEntryId))
        {
            var byId = entries.Where(entry => entry.EntryId == numericEntryId).ToArray();
            return byId.Length == 1
                ? (BenchWarmingTeamLookupOutcome.Available, byId[0].EntryId, null)
                : (BenchWarmingTeamLookupOutcome.NotFound, null, null);
        }

        var exactMatches = entries
            .Where(entry => string.Equals(
                entry.EntryName,
                query,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (exactMatches.Length == 1)
        {
            return (BenchWarmingTeamLookupOutcome.Available, exactMatches[0].EntryId, null);
        }

        if (exactMatches.Length > 1)
        {
            return (
                BenchWarmingTeamLookupOutcome.Ambiguous,
                null,
                ToCandidates(exactMatches.Select(entry => (entry.EntryId, entry.EntryName))));
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
            return (
                BenchWarmingTeamLookupOutcome.Ambiguous,
                null,
                ToCandidates(partial.Select(entry => (entry.EntryId, entry.EntryName))));
        }

        return (BenchWarmingTeamLookupOutcome.Available, partial[0].EntryId, null);
    }

    private static IReadOnlyList<BenchWarmingTeamCandidate> ToCandidates(
        IEnumerable<(int EntryId, string EntryName)> entries)
    {
        return entries
            .Select(entry => new BenchWarmingTeamCandidate(entry.EntryId, entry.EntryName))
            .OrderBy(candidate => candidate.EntryName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static BenchWarmingStreakRecord? FindLongestStreak(
        IEnumerable<BenchWarmingEntryRoundStanding> rounds,
        IReadOnlyDictionary<int, string> latestEntryNames,
        Func<BenchWarmingEntryRoundStanding, bool> qualifies)
    {
        BenchWarmingStreakRecord? best = null;

        foreach (var group in rounds.GroupBy(standing => standing.EntryId))
        {
            var ordered = group
                .OrderBy(standing => standing.EventId)
                .ToArray();
            var latestName = latestEntryNames.GetValueOrDefault(group.Key, ordered[0].EntryName);

            var index = 0;
            while (index < ordered.Length)
            {
                var start = ordered[index];
                if (!qualifies(start))
                {
                    index++;
                    continue;
                }

                var endIndex = index;
                while (endIndex + 1 < ordered.Length &&
                       ordered[endIndex + 1].EventId == ordered[endIndex].EventId + 1 &&
                       qualifies(ordered[endIndex + 1]))
                {
                    endIndex++;
                }

                var length = endIndex - index + 1;
                var record = new BenchWarmingStreakRecord(
                    start.EntryId,
                    latestName,
                    length,
                    start.EventId,
                    ordered[endIndex].EventId);
                best = PickBetterStreak(best, record);

                index = endIndex + 1;
            }
        }

        return best;
    }

    private static bool IsBenchBoost(string? activeChip)
    {
        return string.Equals(activeChip, "bboost", StringComparison.OrdinalIgnoreCase);
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
