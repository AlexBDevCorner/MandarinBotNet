using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DiscordBot.Responses;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace DiscordBot.FantasyPremierLeague;

public sealed class FantasyPremierLeagueClient(
    HttpClient httpClient,
    FantasyPremierLeagueOptions leagueOptions,
    ILogger<FantasyPremierLeagueClient> logger) : IFantasyPremierLeagueClient
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
        CancellationToken cancellationToken)
    {
        return GetAsync<BootstrapStaticResponse>(
            "/api/bootstrap-static/",
            payload => payload.Events is not null,
            cancellationToken);
    }

    public async Task<ClassicStandingsResponse> GetClassicStandingsAsync(
        int leagueId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leagueId);

        var standings = await GetAsync<ClassicStandingsResponse>(
            $"/api/leagues-classic/{leagueId}/standings/",
            payload => payload.Standings?.Results is not null,
            cancellationToken);

        for (var page = 2;
             standings.Standings.HasNext && page <= leagueOptions.MaxStandingsPages;
             page++)
        {
            var nextPage = await GetAsync<ClassicStandingsResponse>(
                $"/api/leagues-classic/{leagueId}/standings/?page_standings={page}",
                payload => payload.Standings?.Results is not null,
                cancellationToken);
            standings.Standings.Results.AddRange(nextPage.Standings.Results);
            standings.Standings.Page = nextPage.Standings.Page;
            standings.Standings.HasNext = nextPage.Standings.HasNext;
        }

        return standings;
    }

    public async Task<HeadToHeadStandingsResponse> GetHeadToHeadStandingsAsync(
        int leagueId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leagueId);

        var standings = await GetAsync<HeadToHeadStandingsResponse>(
            $"/api/leagues-h2h/{leagueId}/standings/",
            payload => payload.HeadToHeadStandings?.Results is not null,
            cancellationToken);

        for (var page = 2;
             standings.HeadToHeadStandings.HasNext && page <= leagueOptions.MaxStandingsPages;
             page++)
        {
            var nextPage = await GetAsync<HeadToHeadStandingsResponse>(
                $"/api/leagues-h2h/{leagueId}/standings/?page_standings={page}",
                payload => payload.HeadToHeadStandings?.Results is not null,
                cancellationToken);
            standings.HeadToHeadStandings.Results.AddRange(
                nextPage.HeadToHeadStandings.Results);
            standings.HeadToHeadStandings.Page =
                nextPage.HeadToHeadStandings.Page;
            standings.HeadToHeadStandings.HasNext =
                nextPage.HeadToHeadStandings.HasNext;
        }

        return standings;
    }

    public Task<EntryEventPicksResponse> GetEntryEventPicksAsync(
        int entryId,
        int eventId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);

        return GetAsync<EntryEventPicksResponse>(
            $"/api/entry/{entryId}/event/{eventId}/picks/",
            payload => payload.Picks is not null,
            cancellationToken);
    }

    public Task<EventLiveResponse> GetEventLiveAsync(
        int eventId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);

        return GetAsync<EventLiveResponse>(
            $"/api/event/{eventId}/live/",
            payload => payload.Elements is not null,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PremierLeagueFixture>> GetFixturesAsync(
        int eventId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);

        return await GetAsync<List<PremierLeagueFixture>>(
            $"/api/fixtures/?event={eventId}",
            fixtures => fixtures.Count > 0 &&
                        fixtures.All(fixture => fixture.EventId == eventId),
            cancellationToken);
    }

    public Task<EntryHistoryResponse> GetEntryHistoryAsync(
        int entryId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryId);

        return GetAsync<EntryHistoryResponse>(
            $"/api/entry/{entryId}/history/",
            payload => payload.Current is not null &&
                       payload.Chips is not null,
            cancellationToken);
    }

    public Task<EntryResponse> GetEntryAsync(
        int entryId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryId);

        return GetAsync<EntryResponse>(
            $"/api/entry/{entryId}/",
            payload => payload.StartedEvent > 0,
            cancellationToken);
    }

    private async Task<T> GetAsync<T>(
        string requestPath,
        Func<T, bool> isValid,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await httpClient.GetAsync(
                requestPath,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateStatusException(requestPath, response.StatusCode);
            }

            T? payload;

            try
            {
                payload = await response.Content.ReadFromJsonAsync<T>(
                    SerializerOptions,
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                throw CreateInvalidPayloadException(requestPath, exception);
            }
            catch (NotSupportedException exception)
            {
                throw CreateInvalidPayloadException(requestPath, exception);
            }

            if (payload is null || !isValid(payload))
            {
                throw CreateInvalidPayloadException(requestPath);
            }

            return payload;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FantasyPremierLeagueApiException)
        {
            throw;
        }
        catch (TimeoutRejectedException exception)
        {
            logger.LogWarning(
                exception,
                "FPL request {RequestPath} exhausted its timeout retries.",
                requestPath);
            throw new FantasyPremierLeagueApiException(
                FantasyPremierLeagueFailureKind.Transient,
                $"The FPL request to '{requestPath}' timed out.",
                innerException: exception);
        }
        catch (OperationCanceledException exception)
        {
            logger.LogWarning(
                exception,
                "FPL request {RequestPath} timed out.",
                requestPath);
            throw new FantasyPremierLeagueApiException(
                FantasyPremierLeagueFailureKind.Transient,
                $"The FPL request to '{requestPath}' timed out.",
                innerException: exception);
        }
        catch (BrokenCircuitException exception)
        {
            logger.LogWarning(
                exception,
                "FPL request {RequestPath} was blocked by the open circuit.",
                requestPath);
            throw new FantasyPremierLeagueApiException(
                FantasyPremierLeagueFailureKind.Transient,
                $"The FPL request to '{requestPath}' was blocked by the open circuit.",
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "FPL request {RequestPath} failed after transient transport retries.",
                requestPath);
            throw new FantasyPremierLeagueApiException(
                FantasyPremierLeagueFailureKind.Transient,
                $"The FPL request to '{requestPath}' failed.",
                exception.StatusCode,
                exception);
        }
    }

    private FantasyPremierLeagueApiException CreateStatusException(
        string requestPath,
        HttpStatusCode statusCode)
    {
        var failureKind = IsTransient(statusCode)
            ? FantasyPremierLeagueFailureKind.Transient
            : FantasyPremierLeagueFailureKind.Permanent;

        logger.LogWarning(
            "FPL request {RequestPath} failed with {FailureKind} HTTP status {StatusCode}.",
            requestPath,
            failureKind,
            (int)statusCode);

        return new FantasyPremierLeagueApiException(
            failureKind,
            $"The FPL request to '{requestPath}' failed with HTTP status {(int)statusCode}.",
            statusCode);
    }

    private FantasyPremierLeagueApiException CreateInvalidPayloadException(
        string requestPath,
        Exception? innerException = null)
    {
        logger.LogWarning(
            innerException,
            "FPL request {RequestPath} returned an invalid payload.",
            requestPath);

        return new FantasyPremierLeagueApiException(
            FantasyPremierLeagueFailureKind.InvalidPayload,
            $"The FPL request to '{requestPath}' returned an invalid payload.",
            innerException: innerException);
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests ||
            (int)statusCode >= 500;
    }
}
