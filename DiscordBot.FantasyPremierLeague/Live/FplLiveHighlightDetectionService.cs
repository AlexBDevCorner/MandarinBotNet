namespace DiscordBot.FantasyPremierLeague.Live;

public sealed class FplLiveHighlightDetectionService(
    FantasyPremierLeagueOptions options)
{
    public IReadOnlyList<FplLiveHighlight> Detect(
        FplLiveGameweek previous,
        FplLiveGameweek current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        if (!string.Equals(previous.Season, current.Season, StringComparison.Ordinal) ||
            previous.EventId != current.EventId)
        {
            throw new ArgumentException(
                "Live highlights can only be detected within the same FPL gameweek.",
                nameof(current));
        }

        var detectedAtUtc = current.CapturedAtUtc.ToUniversalTime();
        var previousByEntry = previous.Managers.ToDictionary(manager => manager.EntryId);
        var highlights = new List<FplLiveHighlight>();
        var previousLeader = previous.Managers.Single(manager => manager.LiveRank == 1);
        var currentLeader = current.Managers.Single(manager => manager.LiveRank == 1);
        var leaderChanged = previousLeader.EntryId != currentLeader.EntryId;

        if (leaderChanged)
        {
            highlights.Add(new LeaderChangedHighlight(
                previousLeader.EntryId,
                previousLeader.EntryName,
                currentLeader.EntryId,
                currentLeader.EntryName,
                currentLeader.LiveTotalPoints,
                GetLeaderGap(current),
                detectedAtUtc));
        }

        foreach (var currentManager in current.Managers)
        {
            if (!previousByEntry.TryGetValue(currentManager.EntryId, out var previousManager))
            {
                continue;
            }

            var rankChange = Math.Abs(previousManager.LiveRank - currentManager.LiveRank);
            var involvedInLeaderChange = leaderChanged &&
                (previousManager.LiveRank == 1 || currentManager.LiveRank == 1);
            if (rankChange >= options.SignificantLiveRankChange &&
                !involvedInLeaderChange)
            {
                highlights.Add(new SignificantRankChangeHighlight(
                    currentManager.EntryId,
                    currentManager.EntryName,
                    previousManager.LiveRank,
                    currentManager.LiveRank,
                    detectedAtUtc));
            }

            if (previousManager.BenchPoints < options.LargeBenchPointsThreshold &&
                currentManager.BenchPoints >= options.LargeBenchPointsThreshold)
            {
                highlights.Add(new BenchThresholdReachedHighlight(
                    currentManager.EntryId,
                    currentManager.EntryName,
                    currentManager.BenchPoints,
                    detectedAtUtc));
            }

            if (previousManager.Captain.CaptainEffectivePoints <
                    options.CaptainSuccessEffectivePointsThreshold &&
                currentManager.Captain.CaptainEffectivePoints >=
                    options.CaptainSuccessEffectivePointsThreshold)
            {
                highlights.Add(new CaptainSuccessHighlight(
                    currentManager.EntryId,
                    currentManager.EntryName,
                    currentManager.Captain.CaptainName,
                    currentManager.Captain.CaptainEffectivePoints,
                    detectedAtUtc));
            }
        }

        var previousDisasters = previous.CaptainDisasters
            .Select(manager => manager.EntryId)
            .ToHashSet();
        foreach (var currentDisaster in current.CaptainDisasters.Where(
                     manager => !previousDisasters.Contains(manager.EntryId)))
        {
            highlights.Add(new CaptainDisasterHighlight(
                currentDisaster.EntryId,
                currentDisaster.EntryName,
                currentDisaster.Captain.CaptainName,
                currentDisaster.Captain.CaptainPoints,
                currentDisaster.Captain.ViceCaptainName,
                currentDisaster.Captain.ViceCaptainPoints,
                detectedAtUtc));
        }

        var previousSubstitutions = previous.AutomaticSubstitutionSalvations
            .Select(CreateSubstitutionIdentity)
            .ToHashSet();
        foreach (var substitution in current.AutomaticSubstitutionSalvations.Where(
                     substitution =>
                         substitution.SavedPoints >=
                            options.AutomaticSubstitutionHighlightPoints &&
                         !previousSubstitutions.Contains(
                             CreateSubstitutionIdentity(substitution))))
        {
            highlights.Add(new AutomaticSubstitutionHighlight(
                substitution.EntryId,
                substitution.EntryName,
                substitution.PlayerOutName,
                substitution.PlayerInName,
                substitution.SavedPoints,
                detectedAtUtc));
        }

        return highlights;
    }

    private static int GetLeaderGap(FplLiveGameweek gameweek)
    {
        return gameweek.Managers
            .Where(manager => manager.LiveRank == 2)
            .Select(manager => manager.GapToLeader)
            .SingleOrDefault();
    }

    private static FplAutomaticSubstitutionIdentity CreateSubstitutionIdentity(
        FplAutomaticSubstitutionSalvation substitution)
    {
        return new FplAutomaticSubstitutionIdentity(
            substitution.EntryId,
            substitution.PlayerOutName,
            substitution.PlayerInName);
    }

    private sealed record FplAutomaticSubstitutionIdentity(
        int EntryId,
        string PlayerOutName,
        string PlayerInName);
}
