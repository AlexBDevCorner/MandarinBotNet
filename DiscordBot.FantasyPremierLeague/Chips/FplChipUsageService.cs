using DiscordBot.Responses;
using Microsoft.Extensions.Logging;

namespace DiscordBot.FantasyPremierLeague.Chips;

public sealed record FplPlayedChip(
    FplChipType Chip,
    int EventId);

public enum FplChipUnavailabilityReason
{
    None,
    UsedInPeriod,
    OpeningGameweek,
    ConsecutiveFreeHit,
    AnotherChipActive
}

public sealed record FplChipAvailability(
    FplChipType Chip,
    bool IsAvailable,
    int? UsedEventId,
    FplChipUrgency Urgency,
    FplChipUnavailabilityReason UnavailabilityReason = FplChipUnavailabilityReason.None,
    FplChipType? BlockingChip = null);

public sealed class FplChipUsageService(
    FplChipSeasonRules seasonRules,
    ILogger<FplChipUsageService> logger)
{
    public IReadOnlyList<FplPlayedChip> MapHistory(IEnumerable<EntryHistoryChip> chips)
    {
        ArgumentNullException.ThrowIfNull(chips);

        var result = new List<FplPlayedChip>();
        foreach (var chip in chips)
        {
            var mapped = TryMap(chip.Name);
            if (mapped is null)
            {
                logger.LogWarning(
                    "Unknown FPL chip name '{ChipName}' at event {EventId} will be ignored.",
                    chip.Name,
                    chip.EventId);
                continue;
            }

            result.Add(new FplPlayedChip(mapped.Value, chip.EventId));
        }

        return result;
    }

    public IReadOnlyList<FplChipAvailability> GetAvailabilities(
        int targetEventId,
        int finalEventId,
        IReadOnlyCollection<FplPlayedChip> history)
    {
        // Backward-compatible overload assumes manager started in GW1.
        return GetAvailabilities(targetEventId, finalEventId, history, managerStartedEventId: 1);
    }

    public IReadOnlyList<FplChipAvailability> GetAvailabilities(
        int targetEventId,
        int finalEventId,
        IReadOnlyCollection<FplPlayedChip> history,
        int managerStartedEventId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetEventId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(finalEventId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(managerStartedEventId);
        ArgumentNullException.ThrowIfNull(history);

        var period = seasonRules.GetPeriod(targetEventId, finalEventId);

        var result = new List<FplChipAvailability>();
        foreach (var chipType in Enum.GetValues<FplChipType>())
        {
            var (isAvailable, usedEventId, reason, blockingChip) = EvaluateAvailability(
                chipType,
                targetEventId,
                managerStartedEventId,
                period,
                history);

            var urgency = seasonRules.GetUrgency(chipType, targetEventId, finalEventId, isAvailable);
            result.Add(new FplChipAvailability(chipType, isAvailable, usedEventId, urgency, reason, blockingChip));
        }

        return result;
    }

    private (bool IsAvailable, int? UsedEventId, FplChipUnavailabilityReason Reason, FplChipType? BlockingChip)
        EvaluateAvailability(
            FplChipType chip,
            int targetEventId,
            int managerStartedEventId,
            FplChipPeriod period,
            IReadOnlyCollection<FplPlayedChip> history)
    {
        // 1. Used in period
        if (IsChipUsedInPeriod(chip, period, history, out var usedInPeriodEventId))
        {
            return (false, usedInPeriodEventId, FplChipUnavailabilityReason.UsedInPeriod, null);
        }

        // 2. Opening Gameweek for WC/FH
        if (targetEventId == managerStartedEventId &&
            (chip == FplChipType.Wildcard || chip == FplChipType.FreeHit))
        {
            return (false, null, FplChipUnavailabilityReason.OpeningGameweek, null);
        }

        // 3. Consecutive Free Hit
        if (chip == FplChipType.FreeHit)
        {
            var blocking = history.FirstOrDefault(h => h.Chip == FplChipType.FreeHit && h.EventId == targetEventId - 1);
            if (blocking is not null)
            {
                return (false, blocking.EventId, FplChipUnavailabilityReason.ConsecutiveFreeHit, FplChipType.FreeHit);
            }
        }

        // 4. Another chip active in target GW
        var activeOther = history.FirstOrDefault(h => h.EventId == targetEventId && h.Chip != chip);
        if (activeOther is not null)
        {
            return (false, activeOther.EventId, FplChipUnavailabilityReason.AnotherChipActive, activeOther.Chip);
        }

        // Also check via season rules for any remaining restrictions (keeps logic centralized)
        if (!seasonRules.CanChipNormallyBePlayed(chip, targetEventId, managerStartedEventId, history))
        {
            // This path is now mostly covered, but keep for safety (e.g., future rules)
            // Determine reason: if opening or consecutive, we already handled, so fallback to generic
            return (false, null, FplChipUnavailabilityReason.OpeningGameweek, null);
        }

        return (true, null, FplChipUnavailabilityReason.None, null);
    }

    private static bool IsChipUsedInPeriod(
        FplChipType chip,
        FplChipPeriod period,
        IReadOnlyCollection<FplPlayedChip> history,
        out int? usedEventId)
    {
        var relevantHistory = history.Where(h => h.Chip == chip).ToList();

        bool InPeriod(int eventId)
        {
            return period == FplChipPeriod.FirstHalf
                ? eventId <= FplChipSeasonRules.FirstHalfLastEvent
                : eventId >= FplChipSeasonRules.SecondHalfFirstEvent;
        }

        var usedInPeriod = relevantHistory.FirstOrDefault(h => InPeriod(h.EventId));
        if (usedInPeriod is not null)
        {
            usedEventId = usedInPeriod.EventId;
            return true;
        }

        usedEventId = null;
        return false;
    }

    private static FplChipType? TryMap(string rawName)
    {
        return rawName.ToLowerInvariant() switch
        {
            "wildcard" => FplChipType.Wildcard,
            "freehit" => FplChipType.FreeHit,
            "bboost" => FplChipType.BenchBoost,
            "3xc" => FplChipType.TripleCaptain,
            _ => null
        };
    }
}
