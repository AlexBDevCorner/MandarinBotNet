using DiscordBot.Responses;

namespace DiscordBot.PremierLeague;

public sealed class WinnerSelectionService
{
    public ClassicStanding? SelectEventWinner(
        IEnumerable<ClassicStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        return standings
            .OrderByDescending(result => result.EventTotal)
            .FirstOrDefault();
    }
}
