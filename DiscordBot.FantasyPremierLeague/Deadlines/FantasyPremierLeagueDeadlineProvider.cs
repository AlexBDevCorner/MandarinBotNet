using DiscordBot.FantasyPremierLeague;
using DiscordBot.PremierLeague;

namespace DiscordBot.Deadlines;

public sealed class FantasyPremierLeagueDeadlineProvider(
    IFantasyPremierLeagueClient premierLeagueClient,
    DeadlineSelectionService deadlineSelection) : IUpcomingDeadlineProvider
{
    public string CompetitionName => "FPL";

    public async Task<CompetitionDeadline?> GetNextAsync(
        CancellationToken cancellationToken)
    {
        var bootstrap = await premierLeagueClient.GetBootstrapStaticAsync(
            cancellationToken);
        var deadline = deadlineSelection.SelectNext(bootstrap.Events);

        return deadline is null
            ? null
            : new CompetitionDeadline(
                "FPL",
                "Gameweek",
                deadline.EventId,
                deadline.DeadlineUtc);
    }
}
