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

    public bool IsNotificationWorthy(
        FplChipRecommendation recommendation,
        FplChipWatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        ArgumentNullException.ThrowIfNull(options);

        return recommendation.OpportunityScore >= options.MinimumNotificationScore &&
               recommendation.Level is FplChipOpportunityLevel.Strong or FplChipOpportunityLevel.VeryStrong;
    }

    public bool HasMeaningfulSignals(
        FplChipWatchReport report,
        FplChipWatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(options);

        foreach (var manager in report.Managers)
        {
            if (manager.FreeHitRecommendation is not null &&
                IsNotificationWorthy(manager.FreeHitRecommendation, options))
            {
                return true;
            }

            if (manager.Chips.Any(c => c.IsAvailable && (c.Urgency == FantasyPremierLeague.Chips.FplChipUrgency.High || c.Urgency == FantasyPremierLeague.Chips.FplChipUrgency.Critical)))
            {
                return true;
            }
        }

        return false;
    }
}
