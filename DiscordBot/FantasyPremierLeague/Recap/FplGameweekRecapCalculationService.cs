using DiscordBot.FantasyPremierLeague.Historical;

namespace DiscordBot.FantasyPremierLeague.Recap;

public sealed class FplGameweekRecapCalculationService
{
    public const int MaxNotableRankChanges = 5;

    public FplGameweekRecap Calculate(FplGameweekSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);

        var managers = snapshot.Managers
            .OrderBy(manager => manager.Rank)
            .ThenBy(manager => manager.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(manager => manager.EntryId)
            .ToArray();
        var highestScore = managers.Max(manager => manager.EventScore);
        var lowestScore = managers.Min(manager => manager.EventScore);
        var biggestClimb = managers.Max(manager =>
            GetEffectiveRankChange(manager) > 0 ? GetEffectiveRankChange(manager) : 0);
        var biggestFall = managers.Min(manager =>
            GetEffectiveRankChange(manager) < 0 ? GetEffectiveRankChange(manager) : 0);
        var captainPerformances = managers
            .Select(manager =>
            {
                var captain = manager.Lineup.Single(pick => pick.IsCaptain);
                return new FplCaptainPerformance(
                    manager,
                    captain,
                    checked(captain.Points * captain.Multiplier));
            })
            .ToArray();
        var bestCaptainPoints = captainPerformances.Max(performance =>
            performance.EffectivePoints);
        var bestBenchPoints = managers.Max(manager => manager.BenchPoints);

        return new FplGameweekRecap(
            snapshot.Season,
            snapshot.EventId,
            highestScore,
            OrderManagers(managers.Where(manager => manager.EventScore == highestScore)),
            lowestScore,
            OrderManagers(managers.Where(manager => manager.EventScore == lowestScore)),
            managers.Average(manager => (decimal)manager.EventScore),
            biggestClimb,
            biggestClimb == 0
                ? []
                : OrderManagers(managers.Where(manager =>
                    GetEffectiveRankChange(manager) == biggestClimb)),
            biggestFall,
            biggestFall == 0
                ? []
                : OrderManagers(managers.Where(manager =>
                    GetEffectiveRankChange(manager) == biggestFall)),
            managers
                .Where(manager => GetEffectiveRankChange(manager) != 0)
                .OrderByDescending(manager => Math.Abs(GetEffectiveRankChange(manager)))
                .ThenByDescending(manager => GetEffectiveRankChange(manager))
                .ThenBy(manager => manager.Rank)
                .ThenBy(manager => manager.EntryName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(manager => manager.EntryId)
                .Take(MaxNotableRankChanges)
                .ToArray(),
            bestBenchPoints == 0
                ? []
                : OrderManagers(managers.Where(manager =>
                    manager.BenchPoints == bestBenchPoints)),
            captainPerformances
                .Where(performance => performance.EffectivePoints == bestCaptainPoints)
                .OrderBy(performance => performance.Manager.Rank)
                .ThenBy(
                    performance => performance.Manager.EntryName,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(performance => performance.Manager.EntryId)
                .ThenBy(
                    performance => performance.Captain.PlayerName,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(performance => performance.Captain.PlayerId)
                .ToArray());
    }

    private static IReadOnlyList<FplManagerGameweekStatistics> OrderManagers(
        IEnumerable<FplManagerGameweekStatistics> managers)
    {
        return managers
            .OrderBy(manager => manager.Rank)
            .ThenBy(manager => manager.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(manager => manager.EntryId)
            .ToArray();
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
