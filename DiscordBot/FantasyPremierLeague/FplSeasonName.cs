using System.Globalization;
using DiscordBot.Responses;

namespace DiscordBot.FantasyPremierLeague;

public static class FplSeasonName
{
    public static string FromEvents(IEnumerable<PremierLeagueEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var firstDeadlineEpoch = events
            .Select(gameweek => gameweek.DeadlineTimeEpoch)
            .Where(deadline => deadline > 0)
            .DefaultIfEmpty()
            .Min();
        if (firstDeadlineEpoch == default)
        {
            throw new InvalidDataException(
                "The FPL bootstrap response did not include a valid gameweek deadline.");
        }

        DateTimeOffset firstDeadline;
        try
        {
            firstDeadline = DateTimeOffset.FromUnixTimeSeconds(firstDeadlineEpoch);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                "The FPL bootstrap response included an invalid gameweek deadline.",
                exception);
        }

        var startYear = firstDeadline.Year;
        var endYear = (startYear + 1) % 100;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{startYear}/{endYear:D2}");
    }
}
