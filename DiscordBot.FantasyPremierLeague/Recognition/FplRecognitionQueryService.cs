using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.Notifications;

namespace DiscordBot.FantasyPremierLeague.Recognition;

public class FplRecognitionQueryService(
    FantasyPremierLeagueOptions options,
    IFplRecognitionStore recognitionStore,
    IFplStatisticsStore statisticsStore)
{
    public virtual FplManagerProfileLookupResult GetManagerProfile(string managerQuery)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managerQuery);

        var leagueId = options.ClassicLeagueId;
        var season = recognitionStore.GetLatestSeason(leagueId);
        if (season is null)
        {
            return FplManagerProfileLookupResult.ForNoData();
        }

        var trackingStartedEventId = recognitionStore.GetFirstCompletedEventId(
            leagueId,
            season);
        if (trackingStartedEventId is null)
        {
            return FplManagerProfileLookupResult.ForNoData();
        }

        var snapshots = statisticsStore.GetSnapshots(season);
        if (snapshots.Count == 0)
        {
            return FplManagerProfileLookupResult.ForNoData();
        }

        var orderedSnapshots = snapshots
            .OrderBy(snapshot => snapshot.EventId)
            .ToArray();
        var latestSnapshot = orderedSnapshots[^1];

        var resolution = ResolveManager(latestSnapshot, managerQuery);
        if (resolution.Outcome == FplManagerProfileLookupOutcome.Ambiguous)
        {
            return FplManagerProfileLookupResult.ForAmbiguous(resolution.Candidates!);
        }

        if (resolution.Outcome == FplManagerProfileLookupOutcome.NotFound)
        {
            return FplManagerProfileLookupResult.ForNotFound();
        }

        var entryId = resolution.Manager!.EntryId;

        var awards = recognitionStore.GetAchievementAwards(leagueId, season);
        var achievementCounts = awards
            .Where(award => award.EntryId == entryId)
            .GroupBy(award => award.AchievementKey)
            .Select(group => new FplAchievementCount(
                group.Key,
                FplAchievementDisplay.GetName(
                    group.Key,
                    group.First().AchievementName),
                group.Count()))
            .OrderByDescending(count => count.Count)
            .ThenBy(count => count.AchievementKey)
            .ToArray();

        var trackedSnapshots = orderedSnapshots
            .Where(snapshot => snapshot.EventId >= trackingStartedEventId.Value &&
                snapshot.Managers.Any(manager => manager.EntryId == entryId))
            .ToArray();

        var gameweekWins = CountGameweekWins(trackedSnapshots, entryId);
        var personalRecords = GetPersonalRecords(trackedSnapshots, entryId);

        var latestManager = latestSnapshot.Managers
            .First(manager => manager.EntryId == entryId);

        var profile = new FplManagerProfile(
            season,
            trackingStartedEventId.Value,
            entryId,
            latestManager.EntryName,
            latestManager.ManagerName,
            latestManager.Rank,
            latestManager.TotalScore,
            gameweekWins,
            personalRecords.BestGameweekScore,
            personalRecords.BestGameweekEventId,
            personalRecords.HighestBenchPoints,
            personalRecords.HighestBenchPointsEventId,
            achievementCounts);

        return FplManagerProfileLookupResult.ForAvailable(profile);
    }

    public virtual FplAchievementSeasonSummary? GetSeasonSummary()
    {
        var leagueId = options.ClassicLeagueId;
        var season = recognitionStore.GetLatestSeason(leagueId);
        if (season is null)
        {
            return null;
        }

        var trackingStartedEventId = recognitionStore.GetFirstCompletedEventId(
            leagueId,
            season);
        if (trackingStartedEventId is null)
        {
            return null;
        }

        var snapshots = statisticsStore.GetSnapshots(season);
        if (snapshots.Count == 0)
        {
            return null;
        }

        var orderedSnapshots = snapshots
            .OrderBy(snapshot => snapshot.EventId)
            .ToArray();
        var latestSnapshot = orderedSnapshots[^1];
        var latestManagers = latestSnapshot.Managers.ToDictionary(
            manager => manager.EntryId);

        var awards = recognitionStore.GetAchievementAwards(leagueId, season);

        var totalAwardRanking = awards
            .GroupBy(award => award.EntryId)
            .Where(group => latestManagers.ContainsKey(group.Key))
            .Select(group =>
            {
                var manager = latestManagers[group.Key];
                return new FplAchievementTotalRankingEntry(
                    group.Key,
                    manager.EntryName,
                    manager.ManagerName,
                    manager.Rank,
                    group.Count());
            })
            .OrderByDescending(entry => entry.TotalAwards)
            .ThenBy(entry => entry.CurrentRank)
            .ThenBy(entry => entry.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.EntryId)
            .ToArray();

        var perAchievementLeaders = BuildPerAchievementLeaders(
            awards,
            latestManagers);

        var trackedSnapshots = orderedSnapshots
            .Where(snapshot => snapshot.EventId >= trackingStartedEventId.Value)
            .ToArray();
        var gameweekWinLeaders = BuildGameweekWinLeaders(
            trackedSnapshots,
            latestManagers,
            out var gameweekWinTopCount);

        return new FplAchievementSeasonSummary(
            season,
            trackingStartedEventId.Value,
            totalAwardRanking,
            perAchievementLeaders,
            gameweekWinTopCount,
            gameweekWinLeaders);
    }

    private static (
        FplManagerProfileLookupOutcome Outcome,
        FplManagerGameweekStatistics? Manager,
        IReadOnlyList<FplManagerReference> Candidates)
        ResolveManager(
            FplGameweekSnapshot latestSnapshot,
            string managerQuery)
    {
        var query = managerQuery.Trim();

        var exactTeam = latestSnapshot.Managers.FirstOrDefault(manager =>
            string.Equals(manager.EntryName, query, StringComparison.OrdinalIgnoreCase));
        if (exactTeam is not null)
        {
            return (FplManagerProfileLookupOutcome.Available, exactTeam, []);
        }

        var exactManager = latestSnapshot.Managers.FirstOrDefault(manager =>
            string.Equals(manager.ManagerName, query, StringComparison.OrdinalIgnoreCase));
        if (exactManager is not null)
        {
            return (FplManagerProfileLookupOutcome.Available, exactManager, []);
        }

        var partialMatches = latestSnapshot.Managers
            .Where(manager =>
                manager.EntryName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                manager.ManagerName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (partialMatches.Length == 0)
        {
            return (FplManagerProfileLookupOutcome.NotFound, null, []);
        }

        if (partialMatches.Length > 1)
        {
            var candidates = partialMatches
                .Select(manager => new FplManagerReference(
                    manager.EntryId,
                    manager.EntryName,
                    manager.ManagerName))
                .ToArray();
            return (FplManagerProfileLookupOutcome.Ambiguous, null, candidates);
        }

        return (FplManagerProfileLookupOutcome.Available, partialMatches[0], []);
    }

    private static int CountGameweekWins(
        IReadOnlyList<FplGameweekSnapshot> trackedSnapshots,
        int entryId)
    {
        var wins = 0;
        foreach (var snapshot in trackedSnapshots)
        {
            var maximumScore = snapshot.Managers.Max(manager => manager.EventScore);
            if (snapshot.Managers.Any(manager =>
                    manager.EntryId == entryId &&
                    manager.EventScore == maximumScore))
            {
                wins++;
            }
        }

        return wins;
    }

    private static (
        int BestGameweekScore,
        int BestGameweekEventId,
        int HighestBenchPoints,
        int HighestBenchPointsEventId)
        GetPersonalRecords(
            IReadOnlyList<FplGameweekSnapshot> trackedSnapshots,
            int entryId)
    {
        var records = trackedSnapshots
            .Select(snapshot =>
            {
                var manager = snapshot.Managers.First(
                    candidate => candidate.EntryId == entryId);
                return (snapshot.EventId, manager.EventScore, manager.BenchPoints);
            })
            .ToArray();

        if (records.Length == 0)
        {
            return (0, 0, 0, 0);
        }

        var bestScore = records
            .OrderByDescending(record => record.EventScore)
            .ThenBy(record => record.EventId)
            .First();
        var bestBench = records
            .OrderByDescending(record => record.BenchPoints)
            .ThenBy(record => record.EventId)
            .First();

        return (
            bestScore.EventScore,
            bestScore.EventId,
            bestBench.BenchPoints,
            bestBench.EventId);
    }

    private static IReadOnlyList<FplAchievementCategoryRanking>
        BuildPerAchievementLeaders(
            IReadOnlyList<FplAchievementAward> awards,
            IReadOnlyDictionary<int, FplManagerGameweekStatistics> latestManagers)
    {
        var categories = new[]
        {
            FplAchievementKeys.BenchWarmer,
            FplAchievementKeys.CaptainDisaster,
            FplAchievementKeys.DifferentialMerchant,
            FplAchievementKeys.MinusEightEnjoyer
        };

        var rankings = new List<FplAchievementCategoryRanking>();
        foreach (var category in categories)
        {
            var countsByManager = awards
                .Where(award => award.AchievementKey == category &&
                    latestManagers.ContainsKey(award.EntryId))
                .GroupBy(award => award.EntryId)
                .ToDictionary(group => group.Key, group => group.Count());
            if (countsByManager.Count == 0)
            {
                continue;
            }

            var maximumCount = countsByManager.Values.Max();
            if (maximumCount == 0)
            {
                continue;
            }

            var leaders = countsByManager
                .Where(pair => pair.Value == maximumCount)
                .Select(pair => new FplManagerReference(
                    pair.Key,
                    latestManagers[pair.Key].EntryName,
                    latestManagers[pair.Key].ManagerName))
                .ToArray();

            rankings.Add(new FplAchievementCategoryRanking(
                category,
                maximumCount,
                leaders));
        }

        return rankings;
    }

    private static IReadOnlyList<FplManagerReference> BuildGameweekWinLeaders(
        IReadOnlyList<FplGameweekSnapshot> trackedSnapshots,
        IReadOnlyDictionary<int, FplManagerGameweekStatistics> latestManagers,
        out int gameweekWinTopCount)
    {
        var winsByManager = new Dictionary<int, int>();
        foreach (var snapshot in trackedSnapshots)
        {
            var maximumScore = snapshot.Managers.Max(manager => manager.EventScore);
            foreach (var manager in snapshot.Managers.Where(
                         manager => manager.EventScore == maximumScore &&
                             latestManagers.ContainsKey(manager.EntryId)))
            {
                winsByManager.TryGetValue(manager.EntryId, out var current);
                winsByManager[manager.EntryId] = current + 1;
            }
        }

        if (winsByManager.Count == 0)
        {
            gameweekWinTopCount = 0;
            return [];
        }

        gameweekWinTopCount = winsByManager.Values.Max();
        var topCount = gameweekWinTopCount;
        return winsByManager
            .Where(pair => pair.Value == topCount &&
                latestManagers.ContainsKey(pair.Key))
            .Select(pair => new FplManagerReference(
                pair.Key,
                latestManagers[pair.Key].EntryName,
                latestManagers[pair.Key].ManagerName))
            .ToArray();
    }
}
