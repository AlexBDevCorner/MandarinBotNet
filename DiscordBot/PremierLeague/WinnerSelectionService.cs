using DiscordBot.Responses;

namespace DiscordBot.PremierLeague;

public sealed class WinnerSelectionService
{
    public IReadOnlyList<ClassicStanding> SelectEventWinners(
        IEnumerable<ClassicStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        var candidates = standings.ToArray();
        if (candidates.Length == 0)
        {
            return [];
        }

        var winningTotal = candidates.Max(result => result.EventTotal);

        return candidates
            .Where(result => result.EventTotal == winningTotal)
            .OrderBy(result => result.Rank)
            .ThenBy(result => result.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Entry)
            .ToArray();
    }
}
