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
    AnotherChipActive,
    EntryMetadataUnavailable
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
        IReadOnlyCollection<FplPlayedChip> history,
        int? managerStartedEventId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetEventId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(finalEventId);
        if (managerStartedEventId is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(managerStartedEventId.Value);
        }

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
            int? managerStartedEventId,
            FplChipPeriod period,
            IReadOnlyCollection<FplPlayedChip> history)
    {
        // 1. Used in period
        if (IsChipUsedInPeriod(chip, period, history, out var usedInPeriodEventId))
        {
            return (false, usedInPeriodEventId, FplChipUnavailabilityReason.UsedInPeriod, null);
        }

        // 2. Opening Gameweek for WC/FH – requires known started_event
        if (managerStartedEventId is null &&
            (chip == FplChipType.Wildcard || chip == FplChipType.FreeHit))
        {
            return (false, null, FplChipUnavailabilityReason.EntryMetadataUnavailable, null);
        }

        if (managerStartedEventId is not null &&
            targetEventId == managerStartedEventId.Value &&
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
        if (managerStartedEventId is not null &&
            !seasonRules.CanChipNormallyBePlayed(chip, targetEventId, managerStartedEventId.Value, history))
        {
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
