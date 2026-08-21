using DiscordBot.Responses;
using Microsoft.Extensions.Logging;

namespace DiscordBot.FantasyPremierLeague.Historical;

public sealed class FplStatisticsCollectionService(
    IFantasyPremierLeagueClient premierLeagueClient,
    FantasyPremierLeagueOptions leagueOptions,
    IFplStatisticsStore store,
    TimeProvider timeProvider,
    ILogger<FplStatisticsCollectionService> logger)
{
    public async Task<FplGameweekSnapshot?> CollectLatestMissingGameweekAsync(
        CancellationToken cancellationToken)
    {
        var bootstrap = await premierLeagueClient.GetBootstrapStaticAsync(
            cancellationToken);
        var season = GetSeason(bootstrap);
        var finishedEvent = SelectLatestMissingEvent(bootstrap, season);
        if (finishedEvent is null)
        {
            logger.LogInformation(
                "Historical FPL statistics collection skipped because the latest " +
                "finished gameweek is already stored for season {Season}.",
                season);
            return null;
        }

        var standings = await premierLeagueClient.GetClassicStandingsAsync(
            leagueOptions.ClassicLeagueId,
            cancellationToken);
        var livePoints = (await premierLeagueClient.GetEventLiveAsync(
                finishedEvent.Id,
                cancellationToken))
            .Elements
            .ToDictionary(element => element.Id, element => element.Stats.TotalPoints);
        var playerNames = bootstrap.Elements
            .ToDictionary(element => element.Id, element => element.WebName);
        var standingsResults = standings.Standings.Results;
        if (standingsResults.Count == 0)
        {
            throw new InvalidDataException(
                $"The FPL standings response contained no managers for season {season} " +
                $"event {finishedEvent.Id}.");
        }

        var managers = new List<FplManagerGameweekStatistics>(standingsResults.Count);
        foreach (var result in standingsResults)
        {
            if (result.Entry <= 0 ||
                string.IsNullOrWhiteSpace(result.EntryName) ||
                string.IsNullOrWhiteSpace(result.PlayerName))
            {
                throw new InvalidDataException(
                    $"The FPL standings response contained incomplete manager data for " +
                    $"season {season} event {finishedEvent.Id}.");
            }

            var picks = await premierLeagueClient.GetEntryEventPicksAsync(
                result.Entry,
                finishedEvent.Id,
                cancellationToken);
            if (picks.Picks.Count == 0)
            {
                throw new InvalidDataException(
                    $"The FPL picks response contained no lineup for entry {result.Entry} " +
                    $"in season {season} event {finishedEvent.Id}.");
            }

            var lineup = picks.Picks
                .Select(pick => CreateLineupPick(
                    pick,
                    playerNames,
                    livePoints,
                    season,
                    finishedEvent.Id,
                    result.Entry))
                .ToList();
            var manager = new FplManagerGameweekStatistics(
                result.Entry,
                result.EntryName,
                result.PlayerName,
                result.EventTotal,
                result.Total,
                result.Rank,
                result.LastRank,
                result.LastRank - result.Rank,
                lineup.Where(pick => pick.IsBench).Sum(pick => pick.Points),
                lineup)
            {
                TransferCost = GetTransferCost(picks, season, finishedEvent.Id, result.Entry)
            };
            managers.Add(manager);
        }

        var snapshot = new FplGameweekSnapshot(
            season,
            finishedEvent.Id,
            DateTimeOffset.FromUnixTimeSeconds(finishedEvent.DeadlineTimeEpoch),
            standings.LastUpdatedData.ToUniversalTime(),
            timeProvider.GetUtcNow().ToUniversalTime(),
            managers);

        try
        {
            store.SaveSnapshot(snapshot);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to persist historical FPL statistics for season {Season} " +
                "event {EventId}; the snapshot was not committed.",
                season,
                finishedEvent.Id);
            throw;
        }

        logger.LogInformation(
            "Persisted historical FPL statistics for season {Season} event {EventId} " +
            "with {ManagerCount} managers and {LineupCount} lineup picks.",
            snapshot.Season,
            snapshot.EventId,
            snapshot.Managers.Count,
            snapshot.Managers.Sum(manager => manager.Lineup.Count));
        return snapshot;
    }

    private PremierLeagueEvent? SelectLatestMissingEvent(
        BootstrapStaticResponse bootstrap,
        string season)
    {
        var finishedEvent = bootstrap.Events
            .Where(item => item.IsFinished)
            .OrderByDescending(item => item.Id)
            .FirstOrDefault();
        if (finishedEvent is null)
        {
            return null;
        }

        try
        {
            return store.IsSnapshotStored(season, finishedEvent.Id)
                ? null
                : finishedEvent;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to check historical FPL statistics storage for season {Season} " +
                "event {EventId}.",
                season,
                finishedEvent.Id);
            throw;
        }
    }

    private static string GetSeason(BootstrapStaticResponse bootstrap)
    {
        if (string.IsNullOrWhiteSpace(bootstrap.SeasonName))
        {
            throw new InvalidDataException(
                "The FPL bootstrap response did not include a season name; refusing to " +
                "persist seasonless historical data.");
        }

        return bootstrap.SeasonName;
    }

    private static FplLineupPick CreateLineupPick(
        EntryEventPick pick,
        IReadOnlyDictionary<int, string> playerNames,
        IReadOnlyDictionary<int, int> livePoints,
        string season,
        int eventId,
        int entryId)
    {
        if (!playerNames.TryGetValue(pick.Element, out var playerName) ||
            string.IsNullOrWhiteSpace(playerName))
        {
            throw new InvalidDataException(
                $"The FPL bootstrap response did not contain player {pick.Element} for " +
                $"season {season} event {eventId} entry {entryId}.");
        }

        if (!livePoints.TryGetValue(pick.Element, out var points))
        {
            throw new InvalidDataException(
                $"The FPL live response did not contain player {pick.Element} for season " +
                $"{season} event {eventId} entry {entryId}.");
        }

        return new FplLineupPick(
            pick.Element,
            playerName,
            pick.Position,
            pick.Multiplier,
            pick.IsCaptain,
            pick.IsViceCaptain,
            points);
    }

    private static int GetTransferCost(
        EntryEventPicksResponse picks,
        string season,
        int eventId,
        int entryId)
    {
        var transferCost = picks.EntryHistory?.EventTransfersCost ?? 0;
        if (transferCost < 0)
        {
            throw new InvalidDataException(
                $"The FPL picks response contained a negative transfer cost for entry " +
                $"{entryId} in season {season} event {eventId}.");
        }

        return transferCost;
    }
}
