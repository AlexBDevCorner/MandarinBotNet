using DiscordBot.Responses;

namespace DiscordBot.FantasyPremierLeague.Live;

public sealed class FplLiveInsightsCalculationService(
    FantasyPremierLeagueOptions options)
{
    private const int LeadingManagerCount = 3;
    private const int MaximumSwingInsights = 5;

    public FplLiveGameweek Calculate(
        string season,
        int eventId,
        DateTimeOffset sourceUpdatedAtUtc,
        DateTimeOffset capturedAtUtc,
        IReadOnlyList<ClassicStanding> standings,
        IReadOnlyDictionary<int, EntryEventPicksResponse> picksByEntry,
        IReadOnlyDictionary<int, PremierLeagueElement> players,
        IReadOnlyDictionary<int, EventLiveElementStats> livePlayers,
        IReadOnlyList<PremierLeagueFixture> fixtures)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(picksByEntry);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(livePlayers);
        ArgumentNullException.ThrowIfNull(fixtures);

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

        var teamStates = CreateTeamStates(fixtures, season, eventId);
        var scores = new List<FplLiveManagerScore>(standings.Count);
        var entryIds = new HashSet<int>();

        foreach (var standing in standings)
        {
            ValidateStanding(standing, season, eventId, entryIds);
            if (!picksByEntry.TryGetValue(standing.Entry, out var picks) ||
                picks is null ||
                picks.Picks is null ||
                picks.Picks.Count == 0)
            {
                throw new InvalidDataException(
                    $"The FPL picks response contained no lineup for entry {standing.Entry} " +
                    $"in season {season} event {eventId}.");
            }

            scores.Add(CalculateManagerScore(
                standing,
                picks,
                players,
                livePlayers,
                teamStates,
                season,
                eventId));
        }

        var orderedScores = OrderManagerScores(scores);
        var managers = CreateManagerInsights(orderedScores);
        var scoresByEntry = scores.ToDictionary(score => score.Standing.Entry);
        var benchAlerts = managers
            .Where(manager => manager.BenchPoints >= options.LargeBenchPointsThreshold)
            .ToArray();
        var captainDisasters = managers
            .Where(manager =>
                scoresByEntry[manager.EntryId].CaptainFixtureFinished &&
                manager.Captain.CaptainPoints <= options.CaptainDisasterPointsThreshold &&
                manager.Captain.ViceCaptainPoints >=
                    options.CaptainDisasterViceCaptainPointsThreshold &&
                manager.Captain.CaptainPoints < manager.Captain.ViceCaptainPoints)
            .ToArray();
        var captainSuccesses = managers
            .Where(manager => manager.Captain.CaptainEffectivePoints >=
                options.CaptainSuccessEffectivePointsThreshold)
            .ToArray();
        var automaticSubstitutionSalvations = managers
            .SelectMany(manager => manager.AutomaticSubstitutionSalvations)
            .ToArray();
        var swingInsights = CreateSwingInsights(orderedScores);

        return new FplLiveGameweek(
            season,
            eventId,
            sourceUpdatedAtUtc.ToUniversalTime(),
            capturedAtUtc.ToUniversalTime(),
            managers,
            benchAlerts,
            captainDisasters,
            captainSuccesses,
            automaticSubstitutionSalvations,
            swingInsights);
    }

    private static FplLiveManagerScore CalculateManagerScore(
        ClassicStanding standing,
        EntryEventPicksResponse picks,
        IReadOnlyDictionary<int, PremierLeagueElement> players,
        IReadOnlyDictionary<int, EventLiveElementStats> livePlayers,
        IReadOnlyDictionary<int, FplTeamLiveState> teamStates,
        string season,
        int eventId)
    {
        if (picks.EntryHistory is null)
        {
            throw new InvalidDataException(
                $"The FPL picks response did not include entry history for entry " +
                $"{standing.Entry} in season {season} event {eventId}.");
        }

        if (picks.EntryHistory.EventTransfersCost < 0)
        {
            throw new InvalidDataException(
                $"The FPL picks response contained a negative transfer cost for entry " +
                $"{standing.Entry} in season {season} event {eventId}.");
        }

        var picksByPlayer = ValidateAndIndexPicks(
            picks.Picks,
            players,
            livePlayers,
            teamStates,
            season,
            eventId,
            standing.Entry);
        var captainPick = picks.Picks.Single(pick => pick.IsCaptain);
        var viceCaptainPick = picks.Picks.Single(pick => pick.IsViceCaptain);
        var effectiveLineup = CalculateEffectiveLineup(
            picks,
            picksByPlayer,
            players,
            livePlayers,
            teamStates,
            season,
            eventId,
            standing.Entry);
        var captainStats = livePlayers[captainPick.Element];
        var viceCaptainStats = livePlayers[viceCaptainPick.Element];
        var captain = new FplLiveCaptainInsights(
            players[captainPick.Element].WebName,
            captainStats.TotalPoints,
            checked(captainStats.TotalPoints *
                effectiveLineup.EffectiveMultipliers[captainPick.Element]),
            players[viceCaptainPick.Element].WebName,
            viceCaptainStats.TotalPoints,
            checked(viceCaptainStats.TotalPoints *
                effectiveLineup.EffectiveMultipliers[viceCaptainPick.Element]));
        var managerAutomaticSubstitutionSalvations =
            CreateAutomaticSubstitutionSalvations(
                standing,
                effectiveLineup.AutomaticSubstitutions,
                players,
                livePlayers);
        var rawLiveGameweekPoints = CalculateRawLiveGameweekPoints(
            picks.Picks,
            effectiveLineup.EffectiveMultipliers,
            livePlayers);
        var transferCost = picks.EntryHistory.EventTransfersCost;
        var liveGameweekPoints = checked(rawLiveGameweekPoints - transferCost);
        var previousTotalPoints = checked(standing.Total - standing.EventTotal);
        var liveTotalPoints = checked(previousTotalPoints + liveGameweekPoints);
        var benchPoints = CalculateBenchPoints(
            picks.Picks,
            effectiveLineup.EffectiveMultipliers,
            livePlayers);
        var playerProgress = CalculatePlayerProgress(
            picks.Picks,
            players,
            teamStates,
            effectiveLineup.EffectiveMultipliers,
            effectiveLineup.EffectiveCaptainElement,
            standing);

        return new FplLiveManagerScore(
            standing,
            previousTotalPoints,
            standing.Total,
            rawLiveGameweekPoints,
            transferCost,
            liveGameweekPoints,
            liveTotalPoints,
            playerProgress.Progress,
            playerProgress.Exposures,
            benchPoints,
            captain,
            managerAutomaticSubstitutionSalvations,
            effectiveLineup.CaptainFixtureFinished);
    }

    private static FplLiveEffectiveLineup CalculateEffectiveLineup(
        EntryEventPicksResponse picks,
        IReadOnlyDictionary<int, EntryEventPick> picksByPlayer,
        IReadOnlyDictionary<int, PremierLeagueElement> players,
        IReadOnlyDictionary<int, EventLiveElementStats> livePlayers,
        IReadOnlyDictionary<int, FplTeamLiveState> teamStates,
        string season,
        int eventId,
        int entryId)
    {
        var captain = picks.Picks.Single(pick => pick.IsCaptain);
        var viceCaptain = picks.Picks.Single(pick => pick.IsViceCaptain);
        var isBenchBoost = string.Equals(
            picks.ActiveChip,
            "bboost",
            StringComparison.OrdinalIgnoreCase);
        var effectiveMultipliers = picks.Picks.ToDictionary(
            pick => pick.Element,
            pick => isBenchBoost || pick.Position <= 11 ? 1 : 0);
        var substitutions = new List<EntryAutomaticSubstitution>();

        if (!isBenchBoost)
        {
            var declaredSubstitutions = picks.AutomaticSubstitutions ?? [];
            if (declaredSubstitutions.Count > 0)
            {
                ApplyAutomaticSubstitutions(
                    declaredSubstitutions,
                    picksByPlayer,
                    effectiveMultipliers,
                    season,
                    eventId,
                    entryId);
                substitutions.AddRange(declaredSubstitutions);
            }
            else
            {
                var projectedSubstitutions = SimulateAutomaticSubstitutions(
                    picks.Picks,
                    players,
                    livePlayers,
                    teamStates);
                ApplyAutomaticSubstitutions(
                    projectedSubstitutions,
                    picksByPlayer,
                    effectiveMultipliers,
                    season,
                    eventId,
                    entryId);
                substitutions.AddRange(projectedSubstitutions);
            }

            foreach (var pick in picks.Picks.Where(pick =>
                         pick.Position <= 11 &&
                         IsConfirmedNotPlaying(
                             pick.Element,
                             players,
                             livePlayers,
                             teamStates)))
            {
                effectiveMultipliers[pick.Element] = 0;
            }
        }

        var captainWasConfirmedNotPlaying = IsConfirmedNotPlaying(
            captain.Element,
            players,
            livePlayers,
            teamStates);
        var captainFixtureFinished =
            teamStates[players[captain.Element].TeamId] == FplTeamLiveState.Finished;
        var effectiveCaptainElement = IsEffectivePlayer(
            captain,
            effectiveMultipliers)
            ? captain.Element
            : 0;
        if (captainWasConfirmedNotPlaying)
        {
            effectiveMultipliers[captain.Element] = 0;
            effectiveCaptainElement = IsEffectivePlayer(
                viceCaptain,
                effectiveMultipliers) &&
                !IsConfirmedNotPlaying(
                    viceCaptain.Element,
                    players,
                    livePlayers,
                    teamStates)
                ? viceCaptain.Element
                : 0;
        }

        var captainMultiplier = GetCaptainMultiplier(picks);
        if (effectiveCaptainElement > 0)
        {
            effectiveMultipliers[effectiveCaptainElement] = captainMultiplier;
        }

        return new FplLiveEffectiveLineup(
            effectiveMultipliers,
            substitutions,
            effectiveCaptainElement,
            captainFixtureFinished);
    }

    private static int GetCaptainMultiplier(EntryEventPicksResponse picks)
    {
        return string.Equals(
            picks.ActiveChip,
            "3xc",
            StringComparison.OrdinalIgnoreCase)
            ? 3
            : 2;
    }

    private static bool IsEffectivePlayer(
        EntryEventPick pick,
        IReadOnlyDictionary<int, int> effectiveMultipliers)
    {
        return effectiveMultipliers[pick.Element] > 0;
    }

    private static void ApplyAutomaticSubstitutions(
        IReadOnlyList<EntryAutomaticSubstitution> substitutions,
        IReadOnlyDictionary<int, EntryEventPick> picksByPlayer,
        IDictionary<int, int> effectiveMultipliers,
        string season,
        int eventId,
        int entryId)
    {
        var substitutedPlayers = new HashSet<int>();
        foreach (var substitution in substitutions)
        {
            // Live payloads can retain original positions, while finalized payloads
            // may already have moved the two players into their effective slots.
            if (substitution is null ||
                substitution.ElementIn == substitution.ElementOut ||
                !substitutedPlayers.Add(substitution.ElementIn) ||
                !substitutedPlayers.Add(substitution.ElementOut) ||
                !picksByPlayer.TryGetValue(substitution.ElementIn, out var playerIn) ||
                !picksByPlayer.TryGetValue(substitution.ElementOut, out var playerOut) ||
                (playerIn.Position <= 11) == (playerOut.Position <= 11))
            {
                throw new InvalidDataException(
                    $"The FPL picks response contained an invalid automatic substitution " +
                    $"for entry {entryId} in season {season} event {eventId}.");
            }

            effectiveMultipliers[playerOut.Element] = 0;
            effectiveMultipliers[playerIn.Element] = 1;
        }
    }

    private static IReadOnlyList<EntryAutomaticSubstitution>
        SimulateAutomaticSubstitutions(
            IReadOnlyList<EntryEventPick> picks,
            IReadOnlyDictionary<int, PremierLeagueElement> players,
            IReadOnlyDictionary<int, EventLiveElementStats> livePlayers,
            IReadOnlyDictionary<int, FplTeamLiveState> teamStates)
    {
        var startingLineup = picks
            .Where(pick => pick.Position <= 11)
            .OrderBy(pick => pick.Position)
            .Select(pick => (pick.Element, ElementType: players[pick.Element].ElementType))
            .ToList();
        var bench = picks
            .Where(pick => pick.Position > 11)
            .OrderBy(pick => pick.Position)
            .ToArray();
        var usedBench = new HashSet<int>();
        var substitutions = new List<EntryAutomaticSubstitution>();

        var goalkeeper = startingLineup.FirstOrDefault(player => player.ElementType == 1);
        var goalkeeperBench = bench.FirstOrDefault(pick =>
            pick.Position == 12 &&
            players[pick.Element].ElementType == 1);
        if (goalkeeper != default &&
            IsConfirmedNotPlaying(goalkeeper.Element, players, livePlayers, teamStates) &&
            goalkeeperBench is not null &&
            HasParticipated(goalkeeperBench, livePlayers))
        {
            ReplaceLineupPlayer(
                startingLineup,
                goalkeeper.Element,
                goalkeeperBench.Element,
                players[goalkeeperBench.Element].ElementType);
            usedBench.Add(goalkeeperBench.Element);
            substitutions.Add(new EntryAutomaticSubstitution
            {
                ElementIn = goalkeeperBench.Element,
                ElementOut = goalkeeper.Element
            });
        }

        foreach (var benchPick in bench.Where(pick => pick.Position > 12))
        {
            if (usedBench.Contains(benchPick.Element) ||
                players[benchPick.Element].ElementType == 1 ||
                !HasParticipated(benchPick, livePlayers))
            {
                continue;
            }

            foreach (var replacement in startingLineup.Where(player =>
                         player.ElementType != 1 &&
                         IsConfirmedNotPlaying(
                             player.Element,
                             players,
                             livePlayers,
                             teamStates)))
            {
                var trialLineup = startingLineup
                    .Select(player => player.Element == replacement.Element
                        ? (benchPick.Element, players[benchPick.Element].ElementType)
                        : player)
                    .ToArray();
                if (!IsValidFormation(trialLineup))
                {
                    continue;
                }

                ReplaceLineupPlayer(
                    startingLineup,
                    replacement.Element,
                    benchPick.Element,
                    players[benchPick.Element].ElementType);
                usedBench.Add(benchPick.Element);
                substitutions.Add(new EntryAutomaticSubstitution
                {
                    ElementIn = benchPick.Element,
                    ElementOut = replacement.Element
                });
                break;
            }
        }

        return substitutions;
    }

    private static void ReplaceLineupPlayer(
        IList<(int Element, int ElementType)> lineup,
        int elementOut,
        int elementIn,
        int elementInType)
    {
        var index = -1;
        for (var currentIndex = 0; currentIndex < lineup.Count; currentIndex++)
        {
            if (lineup[currentIndex].Element == elementOut)
            {
                index = currentIndex;
                break;
            }
        }

        if (index < 0)
        {
            throw new InvalidDataException(
                "The FPL lineup simulation could not find the outgoing player.");
        }

        lineup[index] = (elementIn, elementInType);
    }

    private static bool IsValidFormation(
        IReadOnlyList<(int Element, int ElementType)> lineup)
    {
        var goalkeepers = lineup.Count(player => player.ElementType == 1);
        var defenders = lineup.Count(player => player.ElementType == 2);
        var midfielders = lineup.Count(player => player.ElementType == 3);
        var forwards = lineup.Count(player => player.ElementType == 4);
        return lineup.Count == 11 &&
            goalkeepers == 1 &&
            defenders >= 3 &&
            midfielders >= 2 &&
            forwards >= 1 &&
            defenders + midfielders + forwards == 10;
    }

    private static bool HasParticipated(
        EntryEventPick? pick,
        IReadOnlyDictionary<int, EventLiveElementStats> livePlayers)
    {
        return pick is not null && HasParticipated(livePlayers[pick.Element]);
    }

    private static bool HasParticipated(EventLiveElementStats stats)
    {
        return stats.Minutes > 0 || stats.YellowCards > 0 || stats.RedCards > 0;
    }

    private static bool IsConfirmedNotPlaying(
        int element,
        IReadOnlyDictionary<int, PremierLeagueElement> players,
        IReadOnlyDictionary<int, EventLiveElementStats> livePlayers,
        IReadOnlyDictionary<int, FplTeamLiveState> teamStates)
    {
        return !HasParticipated(livePlayers[element]) &&
            teamStates[players[element].TeamId] == FplTeamLiveState.Finished;
    }

    private static int CalculateRawLiveGameweekPoints(
        IReadOnlyList<EntryEventPick> picks,
        IReadOnlyDictionary<int, int> effectiveMultipliers,
        IReadOnlyDictionary<int, EventLiveElementStats> livePlayers)
    {
        var points = 0;
        foreach (var pick in picks)
        {
            points = checked(points +
                checked(livePlayers[pick.Element].TotalPoints *
                    effectiveMultipliers[pick.Element]));
        }

        return points;
    }

    private static int CalculateBenchPoints(
        IReadOnlyList<EntryEventPick> picks,
        IReadOnlyDictionary<int, int> effectiveMultipliers,
        IReadOnlyDictionary<int, EventLiveElementStats> livePlayers)
    {
        var points = 0;
        foreach (var pick in picks.Where(pick =>
                     pick.Position > 11 && effectiveMultipliers[pick.Element] == 0))
        {
            points = checked(points + livePlayers[pick.Element].TotalPoints);
        }

        return points;
    }

    private static (FplLivePlayerProgress Progress,
        IReadOnlyList<FplLivePlayerExposure> Exposures) CalculatePlayerProgress(
        IReadOnlyList<EntryEventPick> picks,
        IReadOnlyDictionary<int, PremierLeagueElement> players,
        IReadOnlyDictionary<int, FplTeamLiveState> teamStates,
        IReadOnlyDictionary<int, int> effectiveMultipliers,
        int effectiveCaptainElement,
        ClassicStanding standing)
    {
        var playing = 0;
        var yetToPlay = 0;
        var exposures = new List<FplLivePlayerExposure>();
        foreach (var pick in picks.Where(pick => effectiveMultipliers[pick.Element] > 0))
        {
            var state = teamStates[players[pick.Element].TeamId];
            switch (state)
            {
                case FplTeamLiveState.Playing:
                    playing++;
                    break;
                case FplTeamLiveState.YetToPlay:
                    yetToPlay++;
                    break;
                case FplTeamLiveState.Finished:
                    continue;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }

            exposures.Add(new FplLivePlayerExposure(
                pick.Element,
                players[pick.Element].WebName,
                standing.Entry,
                standing.EntryName,
                effectiveMultipliers[pick.Element],
                pick.Element == effectiveCaptainElement));
        }

        return (new FplLivePlayerProgress(playing, yetToPlay), exposures);
    }

    private static IReadOnlyDictionary<int, EntryEventPick> ValidateAndIndexPicks(
        IReadOnlyList<EntryEventPick> picks,
        IReadOnlyDictionary<int, PremierLeagueElement> players,
        IReadOnlyDictionary<int, EventLiveElementStats> livePlayers,
        IReadOnlyDictionary<int, FplTeamLiveState> teamStates,
        string season,
        int eventId,
        int entryId)
    {
        var indexedPicks = new Dictionary<int, EntryEventPick>();
        var positions = new HashSet<int>();
        foreach (var pick in picks)
        {
            if (pick.Element <= 0 ||
                pick.Position <= 0 ||
                pick.Position > 15 ||
                pick.Multiplier < 0 ||
                pick.Multiplier > 3 ||
                (pick.IsCaptain && pick.IsViceCaptain) ||
                !positions.Add(pick.Position))
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
                string.IsNullOrWhiteSpace(player.WebName) ||
                player.TeamId <= 0 ||
                player.ElementType < 1 ||
                player.ElementType > 4)
            {
                throw new InvalidDataException(
                    $"The FPL bootstrap response did not contain valid player metadata for " +
                    $"{pick.Element} for entry {entryId} in season {season} event {eventId}.");
            }

            if (!livePlayers.TryGetValue(pick.Element, out var livePlayer) ||
                livePlayer.Minutes < 0 ||
                livePlayer.YellowCards < 0 ||
                livePlayer.RedCards < 0)
            {
                throw new InvalidDataException(
                    $"The FPL live response did not contain valid player {pick.Element} " +
                    $"data for entry {entryId} in season {season} event {eventId}.");
            }

            if ((pick.Position <= 11 || HasParticipated(livePlayer)) &&
                !teamStates.ContainsKey(player.TeamId))
            {
                throw new InvalidDataException(
                    $"The FPL fixtures response did not contain a fixture for team " +
                    $"{player.TeamId} for entry {entryId} in season {season} event {eventId}.");
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
            IReadOnlyList<EntryAutomaticSubstitution> substitutions,
            IReadOnlyDictionary<int, PremierLeagueElement> players,
            IReadOnlyDictionary<int, EventLiveElementStats> livePlayers)
    {
        var salvations = new List<FplAutomaticSubstitutionSalvation>();
        foreach (var substitution in substitutions)
        {
            var playerInPoints = livePlayers[substitution.ElementIn].TotalPoints;
            var playerOutPoints = livePlayers[substitution.ElementOut].TotalPoints;
            var savedPoints = checked(playerInPoints - playerOutPoints);
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

    private static IReadOnlyDictionary<int, FplTeamLiveState> CreateTeamStates(
        IReadOnlyList<PremierLeagueFixture> fixtures,
        string season,
        int eventId)
    {
        var fixturesByTeam = new Dictionary<int, List<PremierLeagueFixture>>();
        var fixtureIds = new HashSet<int>();
        foreach (var fixture in fixtures)
        {
            if (fixture is null ||
                fixture.Id <= 0 ||
                !fixtureIds.Add(fixture.Id) ||
                fixture.EventId != eventId ||
                fixture.HomeTeamId <= 0 ||
                fixture.AwayTeamId <= 0 ||
                fixture.HomeTeamId == fixture.AwayTeamId ||
                fixture.Started is null ||
                fixture.Finished is null ||
                fixture.Finished == true && fixture.Started != true)
            {
                throw new InvalidDataException(
                    $"The FPL fixtures response contained incomplete or duplicate fixture " +
                    $"data for season {season} event {eventId}.");
            }

            AddFixture(fixturesByTeam, fixture.HomeTeamId, fixture);
            AddFixture(fixturesByTeam, fixture.AwayTeamId, fixture);
        }

        return fixturesByTeam.ToDictionary(
            item => item.Key,
            item => DetermineTeamState(item.Value));
    }

    private static void AddFixture(
        IDictionary<int, List<PremierLeagueFixture>> fixturesByTeam,
        int teamId,
        PremierLeagueFixture fixture)
    {
        if (!fixturesByTeam.TryGetValue(teamId, out var teamFixtures))
        {
            teamFixtures = [];
            fixturesByTeam.Add(teamId, teamFixtures);
        }

        teamFixtures.Add(fixture);
    }

    private static FplTeamLiveState DetermineTeamState(
        IReadOnlyList<PremierLeagueFixture> fixtures)
    {
        if (fixtures.Any(fixture => fixture.Started == true && fixture.Finished == false))
        {
            return FplTeamLiveState.Playing;
        }

        return fixtures.All(fixture => fixture.Finished == true)
            ? FplTeamLiveState.Finished
            : FplTeamLiveState.YetToPlay;
    }

    private static void ValidateStanding(
        ClassicStanding standing,
        string season,
        int eventId,
        HashSet<int> entryIds)
    {
        if (standing is null ||
            standing.Entry <= 0 ||
            string.IsNullOrWhiteSpace(standing.EntryName) ||
            string.IsNullOrWhiteSpace(standing.PlayerName) ||
            standing.Rank <= 0 ||
            standing.LastRank < 0 ||
            standing.EventTotal < 0 ||
            standing.Total < 0 ||
            standing.Total < standing.EventTotal ||
            !entryIds.Add(standing.Entry))
        {
            throw new InvalidDataException(
                $"The FPL standings response contained incomplete or duplicate manager " +
                $"data for season {season} event {eventId}.");
        }
    }

    private static IReadOnlyList<FplLiveManagerScore> OrderManagerScores(
        IEnumerable<FplLiveManagerScore> scores)
    {
        return scores
            .OrderByDescending(score => score.LiveTotalPoints)
            .ThenBy(score => score.Standing.Rank)
            .ThenBy(score => score.Standing.EntryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(score => score.Standing.Entry)
            .ToArray();
    }

    private static IReadOnlyList<FplLiveManagerInsights> CreateManagerInsights(
        IReadOnlyList<FplLiveManagerScore> orderedScores)
    {
        var managers = new List<FplLiveManagerInsights>(orderedScores.Count);
        var leaderPoints = orderedScores[0].LiveTotalPoints;
        var liveRank = 0;
        int? previousLiveTotalPoints = null;
        for (var index = 0; index < orderedScores.Count; index++)
        {
            var score = orderedScores[index];
            if (previousLiveTotalPoints is null ||
                previousLiveTotalPoints.Value != score.LiveTotalPoints)
            {
                liveRank = index + 1;
            }

            var rankChange = score.Standing.LastRank > 0
                ? checked(score.Standing.LastRank - liveRank)
                : 0;
            managers.Add(new FplLiveManagerInsights(
                score.Standing.Entry,
                score.Standing.EntryName,
                score.Standing.PlayerName,
                score.Standing.Rank,
                score.Standing.LastRank,
                liveRank,
                rankChange,
                score.PreviousTotalPoints,
                score.OfficialTotalPoints,
                score.RawLiveGameweekPoints,
                score.TransferCost,
                score.LiveGameweekPoints,
                score.LiveTotalPoints,
                checked(leaderPoints - score.LiveTotalPoints),
                score.PlayerProgress,
                score.BenchPoints,
                score.Captain,
                score.AutomaticSubstitutionSalvations));
            previousLiveTotalPoints = score.LiveTotalPoints;
        }

        return managers;
    }

    private static IReadOnlyList<FplLiveSwingInsight> CreateSwingInsights(
        IReadOnlyList<FplLiveManagerScore> orderedScores)
    {
        var insights = new List<FplLiveSwingInsight>();
        var leadingScores = orderedScores.Take(LeadingManagerCount).ToArray();
        if (leadingScores.Length >= 2)
        {
            var leadingCaptainClash = CreateCaptainClash(
                leadingScores[0],
                leadingScores[1]);
            if (leadingCaptainClash is not null)
            {
                insights.Add(leadingCaptainClash);
            }
        }

        var liveRankByEntry = new Dictionary<int, int>();
        int? previousLiveTotalPoints = null;
        var liveRank = 0;
        for (var index = 0; index < orderedScores.Count; index++)
        {
            var score = orderedScores[index];
            if (previousLiveTotalPoints is null ||
                previousLiveTotalPoints.Value != score.LiveTotalPoints)
            {
                liveRank = index + 1;
            }

            liveRankByEntry.Add(score.Standing.Entry, liveRank);
            previousLiveTotalPoints = score.LiveTotalPoints;
        }
        var uniqueRemainingPlayers = orderedScores
            .SelectMany(score => score.RemainingPlayerExposures)
            .GroupBy(exposure => exposure.PlayerId)
            .Where(group => group.Select(exposure => exposure.EntryId).Distinct().Count() == 1)
            .Select(group => group
                .OrderBy(exposure => liveRankByEntry[exposure.EntryId])
                .ThenBy(exposure => exposure.PlayerName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(exposure => exposure.PlayerId)
                .First())
            .OrderBy(exposure => liveRankByEntry[exposure.EntryId])
            .ThenBy(exposure => exposure.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(exposure => exposure.PlayerId)
            .ToArray();
        var leadingEntryIds = leadingScores
            .Select(score => score.Standing.Entry)
            .ToHashSet();
        foreach (var exposure in uniqueRemainingPlayers.Where(exposure =>
                     leadingEntryIds.Contains(exposure.EntryId)))
        {
            insights.Add(new UniqueRemainingPlayerInsight(
                exposure.EntryId,
                exposure.EntryName,
                exposure.PlayerName));
        }

        for (var firstIndex = 0; firstIndex < leadingScores.Length; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1;
                 secondIndex < leadingScores.Length;
                 secondIndex++)
            {
                if (firstIndex == 0 && secondIndex == 1)
                {
                    continue;
                }

                var captainClash = CreateCaptainClash(
                    leadingScores[firstIndex],
                    leadingScores[secondIndex]);
                if (captainClash is not null)
                {
                    insights.Add(captainClash);
                }
            }
        }

        foreach (var exposure in uniqueRemainingPlayers.Where(exposure =>
                     !leadingEntryIds.Contains(exposure.EntryId)))
        {
            insights.Add(new UniqueRemainingPlayerInsight(
                exposure.EntryId,
                exposure.EntryName,
                exposure.PlayerName));
        }

        return insights.Take(MaximumSwingInsights).ToArray();
    }

    private static CaptainClashInsight? CreateCaptainClash(
        FplLiveManagerScore firstScore,
        FplLiveManagerScore secondScore)
    {
        var firstCaptain = firstScore.RemainingPlayerExposures
            .FirstOrDefault(exposure => exposure.IsCaptain);
        var secondCaptain = secondScore.RemainingPlayerExposures
            .FirstOrDefault(exposure => exposure.IsCaptain);
        if (firstCaptain is null ||
            secondCaptain is null ||
            firstCaptain.PlayerId == secondCaptain.PlayerId)
        {
            return null;
        }

        return new CaptainClashInsight(
            firstScore.Standing.Entry,
            firstScore.Standing.EntryName,
            firstCaptain.PlayerName,
            secondScore.Standing.Entry,
            secondScore.Standing.EntryName,
            secondCaptain.PlayerName);
    }

    private enum FplTeamLiveState
    {
        YetToPlay,
        Playing,
        Finished
    }

    private sealed record FplLiveManagerScore(
        ClassicStanding Standing,
        int PreviousTotalPoints,
        int OfficialTotalPoints,
        int RawLiveGameweekPoints,
        int TransferCost,
        int LiveGameweekPoints,
        int LiveTotalPoints,
        FplLivePlayerProgress PlayerProgress,
        IReadOnlyList<FplLivePlayerExposure> RemainingPlayerExposures,
        int BenchPoints,
        FplLiveCaptainInsights Captain,
        IReadOnlyList<FplAutomaticSubstitutionSalvation>
            AutomaticSubstitutionSalvations,
        bool CaptainFixtureFinished);

    private sealed record FplLiveEffectiveLineup(
        IReadOnlyDictionary<int, int> EffectiveMultipliers,
        IReadOnlyList<EntryAutomaticSubstitution> AutomaticSubstitutions,
        int EffectiveCaptainElement,
        bool CaptainFixtureFinished);
}
