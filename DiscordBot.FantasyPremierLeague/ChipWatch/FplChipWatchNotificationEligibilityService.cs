using DiscordBot;

namespace DiscordBot.FantasyPremierLeague.ChipWatch;

public sealed class FplChipWatchNotificationEligibilityService
{
    public bool IsWithinWindow(
        DateTimeOffset now,
        DateTimeOffset deadline,
        FplChipWatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var remaining = deadline - now;
        var start = TimeSpan.FromHours(options.NotificationWindowStartHours);
        var end = TimeSpan.FromHours(options.NotificationWindowEndHours);

        return remaining <= start && remaining >= end;
    }

    public bool HasMeaningfulSignals(
        FplChipWatchReport report,
        FplChipWatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(options);

        foreach (var manager in report.Managers)
        {
            // FreeHit strong signal
            if (manager.FreeHitRecommendation is not null &&
                manager.FreeHitRecommendation.OpportunityScore >= options.MinimumNotificationScore &&
                manager.FreeHitRecommendation.Level is FplChipOpportunityLevel.Strong or FplChipOpportunityLevel.VeryStrong)
            {
                return true;
            }

            // Expiry high/critical
            if (manager.Chips.Any(c => c.IsAvailable && (c.Urgency == FantasyPremierLeague.Chips.FplChipUrgency.High || c.Urgency == FantasyPremierLeague.Chips.FplChipUrgency.Critical)))
            {
                return true;
            }
        }

        return false;
    }
}
