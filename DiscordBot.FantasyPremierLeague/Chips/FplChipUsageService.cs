using DiscordBot.Responses;
using Microsoft.Extensions.Logging;

namespace DiscordBot.FantasyPremierLeague.Chips;

public sealed record FplPlayedChip(
    FplChipType Chip,
    int EventId);

public sealed record FplChipAvailability(
    FplChipType Chip,
    bool IsAvailable,
    int? UsedEventId,
    FplChipUrgency Urgency);

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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetEventId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(finalEventId);
        ArgumentNullException.ThrowIfNull(history);

        var period = seasonRules.GetPeriod(targetEventId, finalEventId);

        var result = new List<FplChipAvailability>();
        foreach (var chipType in Enum.GetValues<FplChipType>())
        {
            var isAvailable = IsChipAvailable(chipType, targetEventId, period, history, out var usedEventId);

            // Apply GW1 and consecutive FH restrictions
            if (isAvailable && !seasonRules.CanChipNormallyBePlayed(chipType, targetEventId, history))
            {
                isAvailable = false;
            }

            var urgency = seasonRules.GetUrgency(chipType, targetEventId, finalEventId, isAvailable);
            result.Add(new FplChipAvailability(chipType, isAvailable, usedEventId, urgency));
        }

        return result;
    }

    private static bool IsChipAvailable(
        FplChipType chip,
        int targetEventId,
        FplChipPeriod period,
        IReadOnlyCollection<FplPlayedChip> history,
        out int? usedEventId)
    {
        // Determine if any chip of this type was used in the relevant half.
        var relevantHistory = history.Where(h => h.Chip == chip).ToList();

        // Check if consumed in the same period as target.
        // First half: event <= 19
        // Second half: event >= 20
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
            return false;
        }

        // Not used in period => available (subject to other restrictions)
        // Find any used event for display? The spec says UsedEventId should be exposed.
        // If consumed in other half, not relevant to current availability but we still don't expose it.
        // If multiple histories exist, we expose the one in period only.
        usedEventId = null;
        return true;
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
