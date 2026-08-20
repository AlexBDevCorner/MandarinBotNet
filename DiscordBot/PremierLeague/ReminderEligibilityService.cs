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
        DateTimeOffset deadlineUtc,
        string deadline24HoursNotificationType = NotificationTypes.Deadline24Hours,
        string deadline1HourNotificationType = NotificationTypes.Deadline1Hour)
    {
        var remaining = deadlineUtc - nowUtc;

        if (remaining < EarliestDayReminder && remaining > LatestDayReminder)
        {
            return deadline24HoursNotificationType;
        }

        return remaining < EarliestHourReminder && remaining > TimeSpan.Zero
            ? deadline1HourNotificationType
            : null;
    }
}
