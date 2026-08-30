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
        var previousLeaders = FplLiveLeadership.GetLeaders(previous);
        var currentLeaders = FplLiveLeadership.GetLeaders(current);
        var previousLeaderIds = previousLeaders
            .Select(manager => manager.EntryId)
            .ToHashSet();
        var currentLeaderIds = currentLeaders
            .Select(manager => manager.EntryId)
            .ToHashSet();
        var leadershipChanged = !previousLeaderIds.SetEquals(currentLeaderIds);
        var previousLeader = previousLeaders.Count == 1
            ? previousLeaders[0]
            : null;
        var currentLeader = currentLeaders.Count == 1
            ? currentLeaders[0]
            : null;
        if (leadershipChanged &&
            previousLeader is not null &&
            currentLeader is not null)
        {
            highlights.Add(new LeaderChangedHighlight(
                previousLeader.EntryId,
                previousLeader.EntryName,
                currentLeader.EntryId,
                currentLeader.EntryName,
                currentLeader.LiveTotalPoints,
                FplLiveLeadership.GetGapToNextScoreGroup(current, currentLeader),
                detectedAtUtc));
        }

        foreach (var currentManager in current.Managers)
        {
            if (!previousByEntry.TryGetValue(currentManager.EntryId, out var previousManager))
            {
                continue;
            }

            var rankChange = Math.Abs(previousManager.LiveRank - currentManager.LiveRank);
            var involvedInLeaderChange = leadershipChanged &&
                (previousLeaderIds.Contains(currentManager.EntryId) ||
                 currentLeaderIds.Contains(currentManager.EntryId));
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
