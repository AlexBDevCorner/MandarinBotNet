using DiscordBot.FantasyPremierLeague;
using DiscordBot.Responses;
using Microsoft.Extensions.Logging;

namespace DiscordBot.BenchWarming;

public sealed class BenchWarmingLeagueCalculationService(
    IFantasyPremierLeagueClient premierLeagueClient,
    FantasyPremierLeagueOptions leagueOptions,
    IBenchWarmingLeagueStore store,
    TimeProvider timeProvider,
    ILogger<BenchWarmingLeagueCalculationService> logger)
{
    public async Task<BenchWarmingRoundResult?> CalculateLatestFinishedRoundAsync(
        CancellationToken cancellationToken)
    {
        var bootstrap = await premierLeagueClient.GetBootstrapStaticAsync(
            cancellationToken);
        var season = GetSeason(bootstrap);
        var finishedEvent = bootstrap.Events
            .Where(item => item.IsFinished)
            .OrderByDescending(item => item.Id)
            .FirstOrDefault();
        if (finishedEvent is null)
        {
            logger.LogInformation(
                "Bench warming league calculation skipped because no finished event was found for season {Season}.",
                season);
            return null;
        }

        if (store.IsRoundCalculated(season, finishedEvent.Id))
        {
            logger.LogInformation(
                "Bench warming league calculation for season {Season} event {EventId} was already persisted.",
                season,
                finishedEvent.Id);
            return new BenchWarmingRoundResult(
                season,
                finishedEvent.Id,
                WasAlreadyCalculated: true,
                BenchPoints: [],
                RoundStandings: [],
                SeasonStandings: store.GetSeasonStandings(season));
        }

        var standings = await premierLeagueClient.GetClassicStandingsAsync(
            leagueOptions.ClassicLeagueId,
            cancellationToken);
        var livePoints = (await premierLeagueClient.GetEventLiveAsync(
                finishedEvent.Id,
                cancellationToken))
            .Elements
            .ToDictionary(element => element.Id, element => element.Stats.TotalPoints);
        var playerNames = bootstrap.Elements.ToDictionary(
            element => element.Id,
            element => element.WebName);

        var benchPoints = new List<BenchWarmingPlayerPoints>();
        foreach (var result in standings.Standings.Results)
        {
            var picks = await premierLeagueClient.GetEntryEventPicksAsync(
                result.Entry,
                finishedEvent.Id,
                cancellationToken);

            foreach (var pick in picks.Picks.Where(pick => pick.Multiplier == 0))
            {
                benchPoints.Add(new BenchWarmingPlayerPoints(
                    result.Entry,
                    result.EntryName,
                    pick.Element,
                    playerNames.GetValueOrDefault(pick.Element, string.Empty),
                    livePoints.GetValueOrDefault(pick.Element)));
            }
        }

        store.SaveRound(
            season,
            finishedEvent.Id,
            benchPoints,
            timeProvider.GetUtcNow());
        logger.LogInformation(
            "Bench warming league calculated season {Season} event {EventId} with {BenchPlayerCount} benched players.",
            season,
            finishedEvent.Id,
            benchPoints.Count);

        return new BenchWarmingRoundResult(
            season,
            finishedEvent.Id,
            WasAlreadyCalculated: false,
            BenchPoints: benchPoints,
            RoundStandings: Summarize(benchPoints),
            SeasonStandings: store.GetSeasonStandings(season));
    }

    private static string GetSeason(BootstrapStaticResponse bootstrap)
    {
        return FplSeasonName.FromEvents(bootstrap.Events);
    }

    private static IReadOnlyList<BenchWarmingEntryStanding> Summarize(
        IReadOnlyList<BenchWarmingPlayerPoints> benchPoints)
    {
        return benchPoints
            .GroupBy(benchPoint => benchPoint.EntryId)
            .Select(group => new BenchWarmingEntryStanding(
                group.Key,
                group.First().EntryName,
                group.Sum(benchPoint => benchPoint.Points)))
            .OrderByDescending(standing => standing.Points)
            .ThenBy(standing => standing.EntryName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
