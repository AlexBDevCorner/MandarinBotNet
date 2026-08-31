namespace DiscordBot.FantasyPremierLeague.Chips;

public enum FplChipPeriod
{
    FirstHalf,
    SecondHalf
}

public enum FplChipUrgency
{
    None,
    Low,
    High,
    Critical
}

public sealed class FplChipSeasonRules
{
    public const int FirstHalfLastEvent = 19;
    public const int SecondHalfFirstEvent = 20;

    public FplChipPeriod GetPeriod(int targetEventId, int finalEventId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetEventId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(finalEventId);

        return targetEventId <= FirstHalfLastEvent
            ? FplChipPeriod.FirstHalf
            : FplChipPeriod.SecondHalf;
    }

    public bool CanChipNormallyBePlayed(
        FplChipType chip,
        int targetEventId,
        int managerStartedEventId,
        IReadOnlyCollection<FplPlayedChip> history)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetEventId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(managerStartedEventId);
        ArgumentNullException.ThrowIfNull(history);

        // Wildcard and FreeHit cannot be used in the opening Gameweek of that manager's season.
        if (targetEventId == managerStartedEventId &&
            (chip == FplChipType.Wildcard || chip == FplChipType.FreeHit))
        {
            return false;
        }

        // Free Hit cannot be used in consecutive GWs.
        if (chip == FplChipType.FreeHit)
        {
            if (history.Any(h => h.Chip == FplChipType.FreeHit && h.EventId == targetEventId - 1))
            {
                return false;
            }
        }

        return true;
    }

    public FplChipUrgency GetUrgency(
        FplChipType chip,
        int targetEventId,
        int finalEventId,
        bool available)
    {
        if (!available)
        {
            return FplChipUrgency.None;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetEventId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(finalEventId);

        var period = GetPeriod(targetEventId, finalEventId);

        if (period == FplChipPeriod.FirstHalf)
        {
            return targetEventId switch
            {
                17 => FplChipUrgency.Low,
                18 => FplChipUrgency.High,
                19 => FplChipUrgency.Critical,
                _ when targetEventId < 17 => FplChipUrgency.None,
                _ => FplChipUrgency.None
            };
        }

        // Second half
        if (targetEventId == finalEventId - 2)
        {
            return FplChipUrgency.Low;
        }

        if (targetEventId == finalEventId - 1)
        {
            return FplChipUrgency.High;
        }

        if (targetEventId == finalEventId)
        {
            return FplChipUrgency.Critical;
        }

        return FplChipUrgency.None;
    }
}
