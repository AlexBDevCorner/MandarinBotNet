using DiscordBot.FantasyPremierLeague.Historical;

namespace DiscordBot.FantasyPremierLeague.Recognition;

public sealed class FplRatingCalculationService(
    FantasyPremierLeagueOptions options)
{
    private const int MaximumBenchPoints = 10;
    private const int MaximumCaptainFailureGap = 10;
    private const int MaximumTransferCost = 8;
    private const int MaximumRankFall = 5;

    public IReadOnlyList<FplManagerRating> Calculate(
        int leagueId,
        FplGameweekSnapshot snapshot,
        DateTimeOffset calculatedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leagueId);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.Season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(snapshot.EventId);
        ArgumentNullException.ThrowIfNull(snapshot.Managers);
        if (snapshot.Managers.Count == 0)
        {
            throw new InvalidDataException("The snapshot must contain at least one manager.");
        }

        if (calculatedAtUtc == default)
        {
            throw new ArgumentException(
                "The rating calculation must include a calculation timestamp.",
                nameof(calculatedAtUtc));
        }

        return snapshot.Managers
            .OrderBy(manager => manager.Rank)
            .ThenBy(manager => manager.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(manager => manager.EntryId)
            .Select(manager =>
            {
                var captainData = GetCaptainData(manager);
                return new FplManagerRating(
                    leagueId,
                    snapshot.Season,
                    snapshot.EventId,
                    manager.EntryId,
                    manager.EntryName,
                    manager.ManagerName,
                    manager.Rank,
                    CalculateFraudRating(manager, captainData),
                    CalculateMaguireIndex(snapshot.EventId, manager, captainData),
                    options.RecognitionRuleVersion,
                    calculatedAtUtc.ToUniversalTime());
            })
            .ToArray();
    }

    private static int CalculateFraudRating(
        FplManagerGameweekStatistics manager,
        CaptainData captainData)
    {
        var benchComponent = Math.Clamp(manager.BenchPoints, 0, MaximumBenchPoints) * 4m;
        var captainFailureGap = Math.Max(
            captainData.ViceCaptain.Points - captainData.Captain.Points,
            0);
        var captainComponent = Math.Clamp(
            captainFailureGap,
            0,
            MaximumCaptainFailureGap) * 3m;
        var transferComponent = Math.Clamp(
            manager.TransferCost,
            0,
            MaximumTransferCost) * 2.5m;
        var rankFallComponent = Math.Clamp(-manager.RankChange, 0, MaximumRankFall) * 2m;

        return (int)Math.Round(
            benchComponent + captainComponent + transferComponent + rankFallComponent,
            MidpointRounding.AwayFromZero);
    }

    private static int CalculateMaguireIndex(
        int eventId,
        FplManagerGameweekStatistics manager,
        CaptainData captainData)
    {
        var seed = (long)manager.EntryId * 31 +
            (long)eventId * 17 +
            (long)manager.TotalScore * 13 +
            (long)manager.BenchPoints * 7 +
            (long)manager.TransferCost * 5 +
            (long)captainData.Captain.Points * 3 +
            captainData.ViceCaptain.Points;
        var normalized = seed % 101;
        if (normalized < 0)
        {
            normalized += 101;
        }

        return (int)normalized;
    }

    private static CaptainData GetCaptainData(FplManagerGameweekStatistics manager)
    {
        var captains = manager.Lineup.Where(pick => pick.IsCaptain).ToArray();
        var viceCaptains = manager.Lineup.Where(pick => pick.IsViceCaptain).ToArray();
        if (captains.Length != 1 || viceCaptains.Length != 1)
        {
            throw new InvalidDataException(
                $"Manager entry {manager.EntryId} must have exactly one captain and " +
                "vice-captain.");
        }

        return new CaptainData(captains[0], viceCaptains[0]);
    }

    private sealed record CaptainData(
        FplLineupPick Captain,
        FplLineupPick ViceCaptain);
}
