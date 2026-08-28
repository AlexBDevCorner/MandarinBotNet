using DiscordBot;
using DiscordBot.FantasyPremierLeague.Historical;

namespace DiscordBot.FantasyPremierLeague.Recap;

public sealed class FplGameweekRecapCalculationService(FantasyPremierLeagueOptions options)
{
    public const int MaxHeadlineCount = 6;

    public const int MaxSeasonTrends = 2;

    public const int MinLeaderGapReduction = 10;

    public const int RecentWinsWindow = 5;

    public const int MinRecentWins = 3;

    public const int MinConsecutiveRankRises = 3;

    public FplGameweekRecap Calculate(
        FplGameweekSnapshot snapshot,
        IReadOnlyList<FplGameweekSnapshot> seasonHistory)
    {
        ArgumentNullException.ThrowIfNull(seasonHistory);
        ValidateSnapshot(snapshot);

        var managers = snapshot.Managers
            .Order(Comparer<FplManagerGameweekStatistics>.Create(CompareManagers))
            .ToArray();

        var highlights = BuildHighlights(snapshot, managers);
        var seasonTrends = BuildSeasonTrends(snapshot, managers, seasonHistory);
        var standings = managers.Take(5).ToArray();

        return new FplGameweekRecap(
            snapshot.Season,
            snapshot.EventId,
            highlights,
            seasonTrends,
            standings,
            []);
    }

    private IReadOnlyList<FplRecapHighlight> BuildHighlights(
        FplGameweekSnapshot snapshot,
        IReadOnlyList<FplManagerGameweekStatistics> managers)
    {
        var highlights = new List<FplRecapHighlight>();

        var highestScore = managers.Max(manager => manager.EventScore);
        var winners = managers
            .Where(manager => manager.EventScore == highestScore)
            .ToArray();
        highlights.Add(new FplGameweekWinnerHighlight(highestScore, winners));

        var biggestClimb = managers.Max(manager =>
            GetEffectiveRankChange(manager) > 0 ? GetEffectiveRankChange(manager) : 0);
        if (biggestClimb > 0)
        {
            var climber = managers.First(manager =>
                GetEffectiveRankChange(manager) == biggestClimb);
            highlights.Add(new FplRankMovementHighlight(
                FplRecapHighlightKind.BiggestClimb,
                climber,
                climber.LastRank,
                climber.Rank));
        }

        var captainDisaster = SelectCaptainDisaster(managers);
        if (captainDisaster is not null)
        {
            highlights.Add(captainDisaster);
        }

        var transferHit = managers
            .Where(manager => manager.TransferCost >= options.TransferCostAchievementThreshold)
            .OrderDescending(Comparer<FplManagerGameweekStatistics>.Create(CompareByTransferHit))
            .FirstOrDefault();
        if (transferHit is not null)
        {
            highlights.Add(new FplTransferHitHighlight(transferHit, transferHit.TransferCost));
        }

        var benchDisaster = managers
            .Where(manager => manager.BenchPoints >= options.LargeBenchPointsThreshold)
            .OrderDescending(Comparer<FplManagerGameweekStatistics>.Create(CompareByBench))
            .FirstOrDefault();
        if (benchDisaster is not null)
        {
            highlights.Add(new FplBenchDisasterHighlight(benchDisaster));
        }

        var biggestFall = managers.Min(manager =>
            GetEffectiveRankChange(manager) < 0 ? GetEffectiveRankChange(manager) : 0);
        if (biggestFall < 0 && highlights.Count < MaxHeadlineCount)
        {
            var faller = managers.First(manager =>
                GetEffectiveRankChange(manager) == biggestFall);
            highlights.Add(new FplRankMovementHighlight(
                FplRecapHighlightKind.BiggestFall,
                faller,
                faller.LastRank,
                faller.Rank));
        }

        return highlights.Take(MaxHeadlineCount).ToArray();
    }

    private FplCaptainDisasterHighlight? SelectCaptainDisaster(
        IReadOnlyList<FplManagerGameweekStatistics> managers)
    {
        FplCaptainDisasterHighlight? best = null;
        var bestDifference = -1;

        foreach (var manager in managers)
        {
            var pair = GetCaptainPair(manager);
            if (pair is null)
            {
                continue;
            }

            var captainPoints = pair.Value.Captain.Points;
            var viceCaptainPoints = pair.Value.ViceCaptain.Points;
            var meets = captainPoints <= options.CaptainDisasterPointsThreshold &&
                viceCaptainPoints >= options.CaptainDisasterViceCaptainPointsThreshold &&
                captainPoints < viceCaptainPoints;
            if (!meets)
            {
                continue;
            }

            var difference = viceCaptainPoints - captainPoints;
            if (best is null || IsBetterCaptainDisaster(difference, manager, bestDifference, best.Manager))
            {
                best = new FplCaptainDisasterHighlight(manager, pair.Value.Captain, pair.Value.ViceCaptain);
                bestDifference = difference;
            }
        }

        return best;
    }

    private bool IsBetterCaptainDisaster(
        int difference,
        FplManagerGameweekStatistics manager,
        int bestDifference,
        FplManagerGameweekStatistics bestManager)
    {
        if (difference != bestDifference)
        {
            return difference > bestDifference;
        }

        return CompareManagers(manager, bestManager) < 0;
    }

    private IReadOnlyList<FplSeasonTrend> BuildSeasonTrends(
        FplGameweekSnapshot snapshot,
        IReadOnlyList<FplManagerGameweekStatistics> managers,
        IReadOnlyList<FplGameweekSnapshot> seasonHistory)
    {
        var orderedHistory = seasonHistory
            .Where(item => item.Season == snapshot.Season)
            .Order(Comparer<FplGameweekSnapshot>.Create(
                (x, y) => x.EventId.CompareTo(y.EventId)))
            .ToArray();
        var earlierSnapshots = orderedHistory
            .Where(item => item.EventId < snapshot.EventId)
            .ToArray();

        var trends = new List<FplSeasonTrend>();

        var firstTimeAtTop = BuildFirstTimeAtTop(snapshot, managers, orderedHistory, earlierSnapshots);
        if (firstTimeAtTop is not null)
        {
            trends.Add(firstTimeAtTop);
        }

        var recentWins = BuildRecentWins(snapshot, managers, orderedHistory);
        if (recentWins is not null)
        {
            trends.Add(recentWins);
        }

        var consecutiveRises = BuildConsecutiveRankRises(snapshot, managers, earlierSnapshots);
        if (consecutiveRises is not null)
        {
            trends.Add(consecutiveRises);
        }

        var gapReduction = BuildLeaderGapReduction(snapshot, managers, orderedHistory, earlierSnapshots);
        if (gapReduction is not null)
        {
            trends.Add(gapReduction);
        }

        return trends.Take(MaxSeasonTrends).ToArray();
    }

    private FplSeasonTrend? BuildFirstTimeAtTop(
        FplGameweekSnapshot snapshot,
        IReadOnlyList<FplManagerGameweekStatistics> managers,
        IReadOnlyList<FplGameweekSnapshot> orderedHistory,
        IReadOnlyList<FplGameweekSnapshot> earlierSnapshots)
    {
        if (earlierSnapshots.Count == 0)
        {
            return null;
        }

        var leader = managers.FirstOrDefault(manager => manager.Rank == 1);
        if (leader is null)
        {
            return null;
        }

        var wasEverTop = earlierSnapshots.Any(snapshotItem =>
            snapshotItem.Managers.Any(manager =>
                manager.EntryId == leader.EntryId && manager.Rank == 1));
        if (wasEverTop)
        {
            return null;
        }

        var trackingStartedAfterGw1 = orderedHistory[0].EventId > 1;
        return new FplFirstTimeAtTopTrend(leader)
        {
            SinceTrackingStarted = trackingStartedAfterGw1
        };
    }

    private FplSeasonTrend? BuildRecentWins(
        FplGameweekSnapshot snapshot,
        IReadOnlyList<FplManagerGameweekStatistics> managers,
        IReadOnlyList<FplGameweekSnapshot> orderedHistory)
    {
        if (orderedHistory.Count < RecentWinsWindow)
        {
            return null;
        }

        var window = orderedHistory.TakeLast(RecentWinsWindow).ToArray();

        var winCounts = new Dictionary<int, int>();
        foreach (var snapshotItem in window)
        {
            var maxScore = snapshotItem.Managers.Max(manager => manager.EventScore);
            foreach (var manager in snapshotItem.Managers.Where(
                         manager => manager.EventScore == maxScore))
            {
                winCounts[manager.EntryId] =
                    winCounts.GetValueOrDefault(manager.EntryId) + 1;
            }
        }

        FplManagerGameweekStatistics? best = null;
        var bestWins = 0;
        foreach (var manager in managers)
        {
            var wins = winCounts.GetValueOrDefault(manager.EntryId);
            if (wins < MinRecentWins)
            {
                continue;
            }

            if (best is null || wins > bestWins ||
                (wins == bestWins && CompareManagers(manager, best) < 0))
            {
                best = manager;
                bestWins = wins;
            }
        }

        return best is null
            ? null
            : new FplRecentWinsTrend(best, bestWins, RecentWinsWindow);
    }

    private FplSeasonTrend? BuildConsecutiveRankRises(
        FplGameweekSnapshot snapshot,
        IReadOnlyList<FplManagerGameweekStatistics> managers,
        IReadOnlyList<FplGameweekSnapshot> earlierSnapshots)
    {
        var earlierByEvent = new Dictionary<(int EntryId, int EventId), FplManagerGameweekStatistics>();
        foreach (var snapshotItem in earlierSnapshots)
        {
            foreach (var manager in snapshotItem.Managers)
            {
                earlierByEvent[(manager.EntryId, snapshotItem.EventId)] = manager;
            }
        }

        FplManagerGameweekStatistics? best = null;
        var bestStreak = 0;

        foreach (var manager in managers)
        {
            if (GetEffectiveRankChange(manager) <= 0)
            {
                continue;
            }

            var streak = 1;
            var checkEventId = snapshot.EventId - 1;
            while (earlierByEvent.TryGetValue((manager.EntryId, checkEventId), out var previous) &&
                   GetEffectiveRankChange(previous) > 0)
            {
                streak++;
                checkEventId--;
            }

            if (streak < MinConsecutiveRankRises)
            {
                continue;
            }

            if (best is null || streak > bestStreak ||
                (streak == bestStreak && CompareManagers(manager, best) < 0))
            {
                best = manager;
                bestStreak = streak;
            }
        }

        return best is null
            ? null
            : new FplConsecutiveRankRisesTrend(best, bestStreak);
    }

    private FplSeasonTrend? BuildLeaderGapReduction(
        FplGameweekSnapshot snapshot,
        IReadOnlyList<FplManagerGameweekStatistics> managers,
        IReadOnlyList<FplGameweekSnapshot> orderedHistory,
        IReadOnlyList<FplGameweekSnapshot> earlierSnapshots)
    {
        if (earlierSnapshots.Count == 0)
        {
            return null;
        }

        var previousSnapshot = orderedHistory
            .Where(item => item.EventId < snapshot.EventId)
            .Order(Comparer<FplGameweekSnapshot>.Create(
                (x, y) => x.EventId.CompareTo(y.EventId)))
            .Last();
        var previousLeader = previousSnapshot.Managers.FirstOrDefault(manager => manager.Rank == 1);
        var currentLeader = managers.FirstOrDefault(manager => manager.Rank == 1);
        if (previousLeader is null || currentLeader is null)
        {
            return null;
        }

        var previousByEntry = previousSnapshot.Managers.ToDictionary(manager => manager.EntryId);
        FplManagerGameweekStatistics? best = null;
        var bestReduction = 0;
        var bestPreviousGap = 0;
        var bestCurrentGap = 0;

        foreach (var manager in managers)
        {
            if (manager.EntryId == currentLeader.EntryId ||
                !previousByEntry.TryGetValue(manager.EntryId, out var previousManager))
            {
                continue;
            }

            var previousGap = previousLeader.TotalScore - previousManager.TotalScore;
            var currentGap = currentLeader.TotalScore - manager.TotalScore;
            var reduction = previousGap - currentGap;
            if (reduction < MinLeaderGapReduction)
            {
                continue;
            }

            if (best is null || reduction > bestReduction ||
                (reduction == bestReduction && CompareManagers(manager, best) < 0))
            {
                best = manager;
                bestReduction = reduction;
                bestPreviousGap = previousGap;
                bestCurrentGap = currentGap;
            }
        }

        return best is null
            ? null
            : new FplLeaderGapReductionTrend(best, bestPreviousGap, bestCurrentGap);
    }

    private static int CompareManagers(
        FplManagerGameweekStatistics x,
        FplManagerGameweekStatistics y)
    {
        var comparison = x.Rank.CompareTo(y.Rank);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = string.Compare(x.EntryName, y.EntryName, StringComparison.OrdinalIgnoreCase);
        if (comparison != 0)
        {
            return comparison;
        }

        return x.EntryId.CompareTo(y.EntryId);
    }

    private static int CompareByTransferHit(
        FplManagerGameweekStatistics x,
        FplManagerGameweekStatistics y)
    {
        var comparison = x.TransferCost.CompareTo(y.TransferCost);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = y.EventScore.CompareTo(x.EventScore);
        if (comparison != 0)
        {
            return comparison;
        }

        return CompareManagers(x, y);
    }

    private static int CompareByBench(
        FplManagerGameweekStatistics x,
        FplManagerGameweekStatistics y)
    {
        var comparison = x.BenchPoints.CompareTo(y.BenchPoints);
        if (comparison != 0)
        {
            return comparison;
        }

        return CompareManagers(x, y);
    }

    private static (FplLineupPick Captain, FplLineupPick ViceCaptain)? GetCaptainPair(
        FplManagerGameweekStatistics manager)
    {
        var captains = manager.Lineup.Where(pick => pick.IsCaptain).ToArray();
        var viceCaptains = manager.Lineup.Where(pick => pick.IsViceCaptain).ToArray();
        if (captains.Length != 1 || viceCaptains.Length != 1)
        {
            return null;
        }

        return (captains[0], viceCaptains[0]);
    }

    private static bool IsConsecutive(IReadOnlyList<FplGameweekSnapshot> snapshots)
    {
        for (var i = 1; i < snapshots.Count; i++)
        {
            if (snapshots[i].EventId != snapshots[i - 1].EventId + 1)
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateSnapshot(FplGameweekSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (string.IsNullOrWhiteSpace(snapshot.Season))
        {
            throw new InvalidDataException(
                "The FPL gameweek snapshot did not include a season name.");
        }

        if (snapshot.EventId <= 0)
        {
            throw new InvalidDataException(
                "The FPL gameweek snapshot did not include a valid event ID.");
        }

        if (snapshot.Managers is null || snapshot.Managers.Count == 0)
        {
            throw new InvalidDataException(
                $"The FPL gameweek snapshot for season {snapshot.Season} event " +
                $"{snapshot.EventId} did not include any managers.");
        }

        var entryIds = new HashSet<int>();
        foreach (var manager in snapshot.Managers)
        {
            if (manager.EntryId <= 0 ||
                string.IsNullOrWhiteSpace(manager.EntryName) ||
                string.IsNullOrWhiteSpace(manager.ManagerName) ||
                manager.Rank <= 0 ||
                manager.LastRank < 0)
            {
                throw new InvalidDataException(
                    $"The FPL gameweek snapshot contains incomplete manager data for " +
                    $"entry {manager.EntryId}.");
            }

            if (!entryIds.Add(manager.EntryId))
            {
                throw new InvalidDataException(
                    $"The FPL gameweek snapshot contains duplicate manager entry " +
                    $"{manager.EntryId}.");
            }

            if (manager.RankChange != manager.LastRank - manager.Rank)
            {
                throw new InvalidDataException(
                    $"The FPL gameweek snapshot contains an invalid rank change for " +
                    $"entry {manager.EntryId}.");
            }

            if (manager.Lineup is null || manager.Lineup.Count == 0)
            {
                throw new InvalidDataException(
                    $"The FPL gameweek snapshot contains no lineup for entry " +
                    $"{manager.EntryId}.");
            }

            var captains = manager.Lineup.Where(pick => pick.IsCaptain).ToArray();
            if (captains.Length != 1 || captains[0].Multiplier <= 0)
            {
                throw new InvalidDataException(
                    $"The FPL gameweek snapshot contains incomplete captain data for " +
                    $"entry {manager.EntryId}.");
            }

            var calculatedBenchPoints = manager.Lineup
                .Where(pick => pick.IsBench)
                .Sum(pick => pick.Points);
            if (manager.BenchPoints != calculatedBenchPoints)
            {
                throw new InvalidDataException(
                    $"The FPL gameweek snapshot contains inconsistent bench points for " +
                    $"entry {manager.EntryId}.");
            }

            var playerIds = new HashSet<int>();
            foreach (var pick in manager.Lineup)
            {
                if (pick.PlayerId <= 0 ||
                    string.IsNullOrWhiteSpace(pick.PlayerName) ||
                    pick.Position <= 0 ||
                    pick.Multiplier < 0)
                {
                    throw new InvalidDataException(
                        $"The FPL gameweek snapshot contains incomplete lineup data for " +
                        $"entry {manager.EntryId}.");
                }

                if (!playerIds.Add(pick.PlayerId))
                {
                    throw new InvalidDataException(
                        $"The FPL gameweek snapshot contains duplicate player " +
                        $"{pick.PlayerId} for entry {manager.EntryId}.");
                }
            }
        }
    }

    private static int GetEffectiveRankChange(FplManagerGameweekStatistics manager)
    {
        return manager.LastRank == 0 ? 0 : manager.RankChange;
    }
}
