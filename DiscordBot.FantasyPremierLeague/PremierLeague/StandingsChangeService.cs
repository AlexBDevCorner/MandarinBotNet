using DiscordBot.Responses;

namespace DiscordBot.PremierLeague;

public enum StandingsChangeDirection
{
    Up,
    Down
}

public sealed record StandingsChange(
    string EntryName,
    int PositionCount,
    StandingsChangeDirection Direction);

public sealed class StandingsChangeService
{
    public IReadOnlyList<StandingsChange> GetChanges(
        IEnumerable<ClassicStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        return standings
            .Where(result => result.Rank != result.LastRank)
            .Select(result => new StandingsChange(
                result.EntryName,
                Math.Abs(result.Rank - result.LastRank),
                result.Rank < result.LastRank
                    ? StandingsChangeDirection.Up
                    : StandingsChangeDirection.Down))
            .ToArray();
    }
}
