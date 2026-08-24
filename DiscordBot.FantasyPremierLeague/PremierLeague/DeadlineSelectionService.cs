using DiscordBot.Responses;

namespace DiscordBot.PremierLeague;

public sealed record PremierLeagueDeadline(
    int EventId,
    DateTimeOffset DeadlineUtc);

public sealed class DeadlineSelectionService
{
    public PremierLeagueDeadline? SelectNext(
        IEnumerable<PremierLeagueEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var nextEvent = events.FirstOrDefault(item => item.IsNext);

        return nextEvent is null
            ? null
            : new PremierLeagueDeadline(
                nextEvent.Id,
                DateTimeOffset.FromUnixTimeSeconds(nextEvent.DeadlineTimeEpoch));
    }
}
