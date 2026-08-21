using DiscordBot.Responses;

namespace DiscordBot.FantasyPremierLeague.Live;

public sealed class FplLiveInsightsCalculationService(
    FantasyPremierLeagueOptions options)
{
    public FplLiveGameweek Calculate(
        string season,
        int eventId,
        DateTimeOffset sourceUpdatedAtUtc,
        DateTimeOffset capturedAtUtc,
        IReadOnlyList<ClassicStanding> standings,
        IReadOnlyDictionary<int, EntryEventPicksResponse> picksByEntry,
        IReadOnlyDictionary<int, PremierLeagueElement> players,
        IReadOnlyDictionary<int, EventLiveElementStats> livePlayers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(picksByEntry);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(livePlayers);

        if (sourceUpdatedAtUtc == default)
        {
            throw new InvalidDataException(
                "The FPL standings response did not include a source update timestamp.");
        }

        if (capturedAtUtc == default)
        {
            throw new InvalidDataException(
                "The FPL live insights calculation did not include a capture timestamp.");
        }

        if (standings.Count == 0)
        {
            throw new InvalidDataException(
                $"The FPL classic league contained no managers for season {season} " +
                $"event {eventId}.");
        }

        var managers = new List<FplLiveManagerInsights>(standings.Count);
        var entryIds = new HashSet<int>();

        foreach (var standing in standings)
        {
            ValidateStanding(standing, season, eventId, entryIds);
            if (!picksByEntry.TryGetValue(standing.Entry, out var picks) ||
                picks.Picks is null ||
                picks.Picks.Count == 0)
            {
                throw new InvalidDataException(
                    $"The FPL picks response contained no lineup for entry {standing.Entry} " +
                    $"in season {season} event {eventId}.");
            }

            var picksByPlayer = ValidateAndIndexPicks(
                picks.Picks,
                players,
                livePlayers,
                season,
                eventId,
                standing.Entry);
            var captainPick = picks.Picks.Single(pick => pick.IsCaptain);
            var viceCaptainPick = picks.Picks.Single(pick => pick.IsViceCaptain);
            var captainStats = livePlayers[captainPick.Element];
            var viceCaptainStats = livePlayers[viceCaptainPick.Element];
            var captain = new FplLiveCaptainInsights(
                players[captainPick.Element].WebName,
                captainStats.TotalPoints,
                checked(captainStats.TotalPoints * captainPick.Multiplier),
                players[viceCaptainPick.Element].WebName,
                viceCaptainStats.TotalPoints,
                checked(viceCaptainStats.TotalPoints * viceCaptainPick.Multiplier));
            var managerAutomaticSubstitutionSalvations =
                CreateAutomaticSubstitutionSalvations(
                    standing,
                    picks,
                    picksByPlayer,
                    players,
                    livePlayers,
                    season,
                    eventId);
            var livePoints = picks.Picks.Sum(pick =>
                checked(livePlayers[pick.Element].TotalPoints * pick.Multiplier));
            var benchPoints = picks.Picks
                .Where(pick => pick.Position > 11 && pick.Multiplier == 0)
                .Sum(pick => livePlayers[pick.Element].TotalPoints);
            var playersRemainingToPlay = picks.Picks.Count(pick =>
                pick.Position <= 11 && livePlayers[pick.Element].Minutes == 0);

            managers.Add(new FplLiveManagerInsights(
                standing.Entry,
                standing.EntryName,
                standing.PlayerName,
                standing.Rank,
                livePoints,
                playersRemainingToPlay,
                benchPoints,
                captain,
                managerAutomaticSubstitutionSalvations));
        }

        var orderedManagers = OrderManagers(managers);
        var benchAlerts = orderedManagers
            .Where(manager => manager.BenchPoints >= options.LargeBenchPointsThreshold)
            .ToArray();
        var captainDisasters = orderedManagers
            .Where(manager =>
                manager.Captain.CaptainPoints <= options.CaptainDisasterPointsThreshold &&
                manager.Captain.ViceCaptainPoints >=
                    options.CaptainDisasterViceCaptainPointsThreshold &&
                manager.Captain.CaptainPoints < manager.Captain.ViceCaptainPoints)
            .ToArray();
        var captainSuccesses = orderedManagers
            .Where(manager => manager.Captain.CaptainEffectivePoints >=
                options.CaptainSuccessEffectivePointsThreshold)
            .ToArray();
        var automaticSubstitutionSalvations = orderedManagers
            .SelectMany(manager => manager.AutomaticSubstitutionSalvations)
            .ToArray();

        return new FplLiveGameweek(
            season,
            eventId,
            sourceUpdatedAtUtc.ToUniversalTime(),
            capturedAtUtc.ToUniversalTime(),
            orderedManagers,
            benchAlerts,
            captainDisasters,
            captainSuccesses,
            automaticSubstitutionSalvations);
    }

    private static IReadOnlyDictionary<int, EntryEventPick> ValidateAndIndexPicks(
        IReadOnlyList<EntryEventPick> picks,
        IReadOnlyDictionary<int, PremierLeagueElement> players,
        IReadOnlyDictionary<int, EventLiveElementStats> livePlayers,
        string season,
        int eventId,
        int entryId)
    {
        var indexedPicks = new Dictionary<int, EntryEventPick>();
        foreach (var pick in picks)
        {
            if (pick.Element <= 0 ||
                pick.Position <= 0 ||
                pick.Multiplier < 0)
            {
                throw new InvalidDataException(
                    $"The FPL picks response contained incomplete lineup data for entry " +
                    $"{entryId} in season {season} event {eventId}.");
            }

            if (!indexedPicks.TryAdd(pick.Element, pick))
            {
                throw new InvalidDataException(
                    $"The FPL picks response contained duplicate player {pick.Element} " +
                    $"for entry {entryId} in season {season} event {eventId}.");
            }

            if (!players.TryGetValue(pick.Element, out var player) ||
                string.IsNullOrWhiteSpace(player.WebName))
            {
                throw new InvalidDataException(
                    $"The FPL bootstrap response did not contain player {pick.Element} " +
                    $"for entry {entryId} in season {season} event {eventId}.");
            }

            if (!livePlayers.TryGetValue(pick.Element, out var livePlayer) ||
                livePlayer.Minutes < 0)
            {
                throw new InvalidDataException(
                    $"The FPL live response did not contain valid player {pick.Element} " +
                    $"data for entry {entryId} in season {season} event {eventId}.");
            }
        }

        if (picks.Count(pick => pick.IsCaptain) != 1 ||
            picks.Count(pick => pick.IsViceCaptain) != 1)
        {
            throw new InvalidDataException(
                $"The FPL picks response contained incomplete captain data for entry " +
                $"{entryId} in season {season} event {eventId}.");
        }

        var captain = picks.Single(pick => pick.IsCaptain);
        var viceCaptain = picks.Single(pick => pick.IsViceCaptain);
        if (captain.Element == viceCaptain.Element)
        {
            throw new InvalidDataException(
                $"The FPL picks response assigned the same player as captain and vice-captain " +
                $"for entry {entryId} in season {season} event {eventId}.");
        }

        return indexedPicks;
    }

    private static IReadOnlyList<FplAutomaticSubstitutionSalvation>
        CreateAutomaticSubstitutionSalvations(
            ClassicStanding standing,
            EntryEventPicksResponse picks,
            IReadOnlyDictionary<int, EntryEventPick> picksByPlayer,
            IReadOnlyDictionary<int, PremierLeagueElement> players,
            IReadOnlyDictionary<int, EventLiveElementStats> livePlayers,
            string season,
            int eventId)
    {
        var salvations = new List<FplAutomaticSubstitutionSalvation>();
        foreach (var substitution in picks.AutomaticSubstitutions ?? [])
        {
            if (!picksByPlayer.ContainsKey(substitution.ElementIn) ||
                !picksByPlayer.ContainsKey(substitution.ElementOut) ||
                substitution.ElementIn == substitution.ElementOut)
            {
                throw new InvalidDataException(
                    $"The FPL picks response contained an invalid automatic substitution " +
                    $"for entry {standing.Entry} in season {season} event {eventId}.");
            }

            var playerInPoints = livePlayers[substitution.ElementIn].TotalPoints;
            var playerOutPoints = livePlayers[substitution.ElementOut].TotalPoints;
            var savedPoints = playerInPoints - playerOutPoints;
            if (savedPoints <= 0)
            {
                continue;
            }

            salvations.Add(new FplAutomaticSubstitutionSalvation(
                standing.Entry,
                standing.EntryName,
                players[substitution.ElementIn].WebName,
                playerInPoints,
                players[substitution.ElementOut].WebName,
                playerOutPoints,
                savedPoints));
        }

        return salvations
            .OrderByDescending(salvation => salvation.SavedPoints)
            .ThenBy(salvation => salvation.PlayerInName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ValidateStanding(
        ClassicStanding standing,
        string season,
        int eventId,
        HashSet<int> entryIds)
    {
        if (standing.Entry <= 0 ||
            string.IsNullOrWhiteSpace(standing.EntryName) ||
            string.IsNullOrWhiteSpace(standing.PlayerName) ||
            standing.Rank <= 0 ||
            !entryIds.Add(standing.Entry))
        {
            throw new InvalidDataException(
                $"The FPL standings response contained incomplete or duplicate manager " +
                $"data for season {season} event {eventId}.");
        }
    }

    private static IReadOnlyList<FplLiveManagerInsights> OrderManagers(
        IEnumerable<FplLiveManagerInsights> managers)
    {
        return managers
            .OrderBy(manager => manager.Rank)
            .ThenBy(manager => manager.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(manager => manager.EntryId)
            .ToArray();
    }
}
