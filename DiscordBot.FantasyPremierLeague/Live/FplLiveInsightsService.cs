using DiscordBot.Responses;
using Microsoft.Extensions.Logging;

namespace DiscordBot.FantasyPremierLeague.Live;

public sealed class FplLiveInsightsService(
    IFantasyPremierLeagueClient premierLeagueClient,
    FantasyPremierLeagueOptions options,
    FplLiveInsightsCalculationService calculationService,
    TimeProvider timeProvider,
    ILogger<FplLiveInsightsService> logger)
{
    public async Task<FplLiveInsightsResult> GetCurrentAsync(
        CancellationToken cancellationToken)
    {
        var capturedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        string? season = null;
        var eventId = 0;

        try
        {
            var bootstrap = await premierLeagueClient.GetBootstrapStaticAsync(
                cancellationToken);
            season = GetSeason(bootstrap);
            var activeGameweek = bootstrap.Events
                .Where(item => item.IsCurrent)
                .OrderByDescending(item => item.Id)
                .FirstOrDefault();
            if (activeGameweek is null)
            {
                logger.LogInformation(
                    "FPL live insights skipped because no active gameweek was found for " +
                    "season {Season}.",
                    season);
                return FplLiveInsightsResult.NoActiveGameweek();
            }

            eventId = activeGameweek.Id;
            if (eventId <= 0)
            {
                throw new InvalidDataException(
                    "The FPL bootstrap response did not include a valid active gameweek.");
            }

            var standings = await premierLeagueClient.GetClassicStandingsAsync(
                options.ClassicLeagueId,
                cancellationToken);
            if (standings.Standings is null || standings.Standings.Results is null)
            {
                throw new InvalidDataException(
                    $"The FPL standings response did not include managers for season " +
                    $"{season} event {eventId}.");
            }

            if (standings.Standings.HasNext)
            {
                throw new InvalidDataException(
                    $"The FPL standings response exceeded the configured page limit for " +
                    $"season {season} event {eventId}.");
            }

            var sourceUpdatedAtUtc = standings.LastUpdatedData.ToUniversalTime();
            if (sourceUpdatedAtUtc == default)
            {
                throw new InvalidDataException(
                    $"The FPL standings response did not include a source update timestamp " +
                    $"for season {season} event {eventId}.");
            }

            var standingsAge = capturedAtUtc - sourceUpdatedAtUtc;
            if (standingsAge < TimeSpan.Zero)
            {
                logger.LogWarning(
                    "The FPL league standings snapshot timestamp is in the future for " +
                    "season {Season} event {EventId}; it is retained as standings metadata " +
                    "only and does not gate the live calculation.",
                    season,
                    eventId);
            }
            else
            {
                logger.LogInformation(
                    "FPL league standings snapshot for season {Season} event {EventId} is " +
                    "{StandingsAgeMinutes} minutes old; it is retained as standings metadata " +
                    "only and does not gate the live calculation.",
                    season,
                    eventId,
                    Math.Round(standingsAge.TotalMinutes, 1));
            }

            var fixtures = await premierLeagueClient.GetFixturesAsync(
                eventId,
                cancellationToken);
            var live = await premierLeagueClient.GetEventLiveAsync(
                eventId,
                cancellationToken);
            if (live.Elements is null)
            {
                throw new InvalidDataException(
                    $"The FPL live response did not include player data for season {season} " +
                    $"event {eventId}.");
            }

            var livePlayers = live.Elements.ToDictionary(
                element => element.Id,
                element => element.Stats ?? throw new InvalidDataException(
                    $"The FPL live response did not include stats for player " +
                    $"{element.Id} in season {season} event {eventId}."));
            var players = bootstrap.Elements.ToDictionary(
                element => element.Id,
                element => element);
            var picksByEntry = new Dictionary<int, EntryEventPicksResponse>();
            foreach (var standing in standings.Standings.Results)
            {
                picksByEntry[standing.Entry] =
                    await premierLeagueClient.GetEntryEventPicksAsync(
                        standing.Entry,
                        eventId,
                        cancellationToken);
            }

            var gameweek = calculationService.Calculate(
                season,
                eventId,
                sourceUpdatedAtUtc,
                capturedAtUtc,
                standings.Standings.Results,
                picksByEntry,
                players,
                livePlayers,
                fixtures);
            logger.LogInformation(
                "Calculated FPL live insights for season {Season} event {EventId} with " +
                "{ManagerCount} managers, {BenchAlertCount} bench alerts, " +
                "{CaptainDisasterCount} captain disasters, and {CaptainSuccessCount} " +
                "captain successes.",
                gameweek.Season,
                gameweek.EventId,
                gameweek.Managers.Count,
                gameweek.BenchAlerts.Count,
                gameweek.CaptainDisasters.Count,
                gameweek.CaptainSuccesses.Count);
            return FplLiveInsightsResult.Available(gameweek);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FantasyPremierLeagueApiException exception)
        {
            logger.LogWarning(
                exception,
                "FPL live insights failed for season {Season} event {EventId} with " +
                "failure kind {FailureKind} and HTTP status {StatusCode}.",
                season ?? "unknown",
                eventId,
                exception.FailureKind,
                exception.StatusCode);
            return FplLiveInsightsResult.Unavailable(exception.FailureKind);
        }
        catch (InvalidDataException exception)
        {
            logger.LogWarning(
                exception,
                "FPL live insights skipped invalid or stale source data for season " +
                "{Season} event {EventId}.",
                season ?? "unknown",
                eventId);
            return FplLiveInsightsResult.Unavailable(
                FantasyPremierLeagueFailureKind.InvalidPayload);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "FPL live insights failed unexpectedly for season {Season} event {EventId}.",
                season ?? "unknown",
                eventId);
            return FplLiveInsightsResult.Unavailable();
        }
    }

    private static string GetSeason(BootstrapStaticResponse bootstrap)
    {
        if (bootstrap.Events is null || bootstrap.Events.Count == 0)
        {
            throw new InvalidDataException(
                "The FPL bootstrap response did not include gameweeks.");
        }

        if (bootstrap.Elements is null || bootstrap.Elements.Count == 0)
        {
            throw new InvalidDataException(
                "The FPL bootstrap response did not include players.");
        }

        return FplSeasonName.FromEvents(bootstrap.Events);
    }
}
