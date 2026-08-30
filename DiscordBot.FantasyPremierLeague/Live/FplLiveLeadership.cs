namespace DiscordBot.FantasyPremierLeague.Live;

internal static class FplLiveLeadership
{
    internal static IReadOnlyList<FplLiveManagerInsights> GetLeaders(
        FplLiveGameweek gameweek)
    {
        ArgumentNullException.ThrowIfNull(gameweek);
        if (gameweek.Managers.Count == 0)
        {
            throw new InvalidDataException(
                "The FPL live gameweek does not contain any managers.");
        }

        var leaderPoints = gameweek.Managers.Max(manager => manager.LiveTotalPoints);
        return gameweek.Managers
            .Where(manager => manager.LiveTotalPoints == leaderPoints)
            .ToArray();
    }

    internal static int GetGapToNextScoreGroup(
        FplLiveGameweek gameweek,
        FplLiveManagerInsights leader)
    {
        ArgumentNullException.ThrowIfNull(gameweek);
        ArgumentNullException.ThrowIfNull(leader);

        var nextScore = gameweek.Managers
            .Where(manager => manager.LiveTotalPoints < leader.LiveTotalPoints)
            .Select(manager => (int?)manager.LiveTotalPoints)
            .Max();
        return nextScore is null
            ? 0
            : checked(leader.LiveTotalPoints - nextScore.Value);
    }
}
