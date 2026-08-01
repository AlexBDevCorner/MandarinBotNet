using DiscordBot.Notifications;

namespace DiscordBot.PremierLeague;

public sealed class ReminderEligibilityService
{
    private static readonly TimeSpan EarliestDayReminder = TimeSpan.FromHours(24)
        .Add(TimeSpan.FromMinutes(58));
    private static readonly TimeSpan LatestDayReminder = TimeSpan.FromHours(23)
        .Add(TimeSpan.FromMinutes(2));
    private static readonly TimeSpan EarliestHourReminder = TimeSpan.FromMinutes(70);

    public string? SelectNotificationType(
        DateTimeOffset nowUtc,
        DateTimeOffset deadlineUtc)
    {
        var remaining = deadlineUtc - nowUtc;

        if (remaining < EarliestDayReminder && remaining > LatestDayReminder)
        {
            return NotificationTypes.Deadline24Hours;
        }

        return remaining < EarliestHourReminder && remaining > TimeSpan.Zero
            ? NotificationTypes.Deadline1Hour
            : null;
    }
}
