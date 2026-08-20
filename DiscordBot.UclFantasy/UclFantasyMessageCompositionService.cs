using System.Globalization;
using DiscordBot.PremierLeague;

namespace DiscordBot.UclFantasy;

public sealed class UclFantasyMessageCompositionService(
    ConfiguredTimeZone configuredTimeZone)
{
    public string ComposeDeadlineReminder(
        DateTimeOffset deadlineUtc,
        DateTimeOffset utcNow)
    {
        var localDeadline = configuredTimeZone.FromUtc(deadlineUtc);
        var remaining = deadlineUtc - utcNow;
        var remainingMinutes = Math.Max(0, (int)remaining.TotalMinutes);
        var remainingHours = remainingMinutes / 60;
        var minutes = remainingMinutes % 60;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"UCL Matchday deadline: {localDeadline:dd MMMM yyyy, HH:mm} " +
            $"(Riga, Latvia). Time remaining: {remainingHours}h {minutes}m.");
    }
}
