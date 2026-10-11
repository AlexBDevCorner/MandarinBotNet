using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace DiscordBot.UclFantasy;

public sealed class UclFantasyClient(
    HttpClient httpClient,
    UclFantasyClientOptions options,
    ILogger<UclFantasyClient> logger) : IUclFantasyClient
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public Task<UclFantasyWebConfigurationResponse> GetWebConfigurationAsync(
        CancellationToken cancellationToken)
    {
        return GetAsync<UclFantasyWebConfigurationResponse>(
            new Uri(options.WebConfigurationPath, UriKind.Relative),
            payload => payload.Data?.Value?.TourId > 0,
            cancellationToken);
    }

    public Task<UclFantasyFixturesResponse> GetFixturesAsync(
        UclFantasyWebConfigurationValue webConfiguration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webConfiguration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(webConfiguration.TourId);

        var fixtureUri = BuildFixturesUri(webConfiguration);
        return GetAsync<UclFantasyFixturesResponse>(
            fixtureUri,
            payload => payload.Data?.Value is not null,
            cancellationToken);
    }

    public async Task<UclLeagueStandings> GetLeagueStandingsAsync(
        UclLeagueStandingsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.LeagueId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.OptType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.PhaseId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.MatchdayId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.PageSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.MaxPages);
        if (query.PageSize > 50)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The UCL standings page size must not exceed 50 entries per request.");
        }

        string? leagueName = null;
        var collected = new List<UclLeagueStanding>();
        var seenIds = new HashSet<long>();
        int? expectedTotal = null;

        for (var page = 1; page <= query.MaxPages; page++)
        {
            var requestUri = BuildLeagueStandingsUri(query, page);
            var payload = await GetAsync<UclFantasyLeagueStandingsResponse>(
                requestUri,
                response => response.Data?.Value is not null,
                cancellationToken);

            var value = payload.Data!.Value!;
            var pageMembers = value.ResolvedMembers();
            if (pageMembers.Count == 0)
            {
                throw CreateInvalidPayloadException(requestUri.OriginalString);
            }

            var responseLeagueId = value.ResolvedLeagueId;
            if (responseLeagueId != 0 && responseLeagueId != query.LeagueId)
            {
                throw CreateInvalidPayloadException(requestUri.OriginalString);
            }

            var responsePhase = value.PhaseId ?? query.PhaseId;
            var responseMatchday = value.MatchdayId ?? value.MatchdayIdShort ?? query.MatchdayId;
            if (responsePhase != query.PhaseId || responseMatchday != query.MatchdayId)
            {
                throw CreateInvalidPayloadException(requestUri.OriginalString);
            }

            leagueName ??= value.ResolvedLeagueName;

            var total = value.ResolvedTotalMembers;
            if (total.HasValue)
            {
                if (total.Value < 0)
                {
                    throw CreateInvalidPayloadException(requestUri.OriginalString);
                }

                if (expectedTotal.HasValue && expectedTotal.Value != total.Value)
                {
                    throw CreateInvalidPayloadException(requestUri.OriginalString);
                }

                expectedTotal = total.Value;
            }

            foreach (var member in pageMembers)
            {
                var standing = NormalizeMember(member, requestUri.OriginalString);
                if (seenIds.Add(standing.StableId))
                {
                    collected.Add(standing);
                }
            }

            if (expectedTotal.HasValue)
            {
                if (collected.Count > expectedTotal.Value)
                {
                    throw CreateInvalidPayloadException(requestUri.OriginalString);
                }

                if (collected.Count == expectedTotal.Value)
                {
                    break;
                }
            }

            if (pageMembers.Count < query.PageSize)
            {
                break;
            }

            if (page == query.MaxPages)
            {
                throw CreateInvalidPayloadException(requestUri.OriginalString);
            }
        }

        if (collected.Count == 0)
        {
            throw CreateInvalidPayloadException(
                $"services/api/Leagues/{query.LeagueId}/leagueleaderboard");
        }

        if (expectedTotal.HasValue && collected.Count != expectedTotal.Value)
        {
            throw CreateInvalidPayloadException(
                $"services/api/Leagues/{query.LeagueId}/leagueleaderboard");
        }

        var ordered = collected
            .OrderBy(member => member.Rank <= 0 ? int.MaxValue : member.Rank)
            .ThenBy(member => member.StableId)
            .ThenBy(member => member.TeamName, StringComparer.Ordinal)
            .ToList();

        // Backfill missing ranks from deterministic order so ties keep the
        // source rank while rank-less payloads still render sensibly.
        var normalized = new List<UclLeagueStanding>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            var member = ordered[index];
            normalized.Add(member.Rank > 0
                ? member
                : member with { Rank = index + 1 });
        }

        return new UclLeagueStandings(
            query.LeagueId,
            string.IsNullOrWhiteSpace(leagueName) ? $"League {query.LeagueId}" : leagueName!,
            query.PhaseId,
            query.MatchdayId,
            normalized);
    }

    private static UclLeagueStanding NormalizeMember(
        UclFantasyLeagueStandingsMemberPayload member,
        string requestPath)
    {
        var stableId = member.EntryId ?? member.Entry ?? member.TeamId ?? member.Id ?? member.UserId;
        if (stableId is not <= 0 && stableId is not null)
        {
            // Valid stable identity below.
        }
        else
        {
            throw new UclFantasyApiException(
                UclFantasyFailureKind.InvalidPayload,
                $"The UCL Fantasy request to '{requestPath}' returned a standings member without a stable identity.");
        }

        var teamName = FirstNonEmpty(
            member.TeamName,
            member.EntryName,
            member.Name) ?? string.Empty;
        var managerName = FirstNonEmpty(
            member.ManagerName,
            member.PlayerName,
            member.UserName,
            member.CoachName) ?? string.Empty;
        var rank = member.Rank ?? member.Position ?? member.PositionShort ?? member.Standing ?? 0;
        var lastRank = member.LastRank ?? member.PreviousRank ?? member.PreviousRankShort ?? member.LastRankSnake ?? 0;
        var total = member.Total ?? member.TotalPoints ?? member.Points ?? member.OverallPoints ?? 0;
        var matchday = member.EventTotal ?? member.MatchdayPoints ?? member.GameweekPoints ??
            member.RoundPoints ?? member.WeekPoints ?? member.LastMatchdayPoints ?? 0;

        if (rank < 0 || lastRank < 0)
        {
            throw new UclFantasyApiException(
                UclFantasyFailureKind.InvalidPayload,
                $"The UCL Fantasy request to '{requestPath}' returned an invalid standings rank.");
        }

        return new UclLeagueStanding(
            stableId.Value,
            teamName.Trim(),
            managerName.Trim(),
            rank,
            lastRank,
            total,
            matchday);
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Uri BuildLeagueStandingsUri(UclLeagueStandingsQuery query, int page)
    {
        // Observed anonymous endpoint. The buster query value is a
        // cache-busting nonce, not an auth token, and is intentionally
        // omitted: requests without it succeed and stay moderate. vPageChunk
        // and vPageOneChunk both carry the page size in the observed URL, so
        // both are sent for compatibility.
        var path = string.Concat(
            "services/api/Leagues/",
            query.LeagueId.ToString(CultureInfo.InvariantCulture),
            "/leagueleaderboard?optType=",
            query.OptType.ToString(CultureInfo.InvariantCulture),
            "&phaseId=",
            query.PhaseId.ToString(CultureInfo.InvariantCulture),
            "&matchdayId=",
            query.MatchdayId.ToString(CultureInfo.InvariantCulture),
            "&vPageChunk=",
            query.PageSize.ToString(CultureInfo.InvariantCulture),
            "&vPageNo=",
            page.ToString(CultureInfo.InvariantCulture),
            "&vPageOneChunk=",
            query.PageSize.ToString(CultureInfo.InvariantCulture),
            "&leagueID=",
            query.LeagueId.ToString(CultureInfo.InvariantCulture));
        return new Uri(path, UriKind.Relative);
    }

    private async Task<T> GetAsync<T>(
        Uri requestUri,
        Func<T, bool> isValid,
        CancellationToken cancellationToken)
        where T : class
    {
        var requestPath = requestUri.IsAbsoluteUri
            ? requestUri.PathAndQuery
            : requestUri.OriginalString;

        try
        {
            using var response = await httpClient.GetAsync(
                requestUri,
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
        catch (UclFantasyApiException)
        {
            throw;
        }
        catch (TimeoutRejectedException exception)
        {
            logger.LogWarning(
                exception,
                "UCL Fantasy request {RequestPath} exhausted its timeout retries.",
                requestPath);
            throw new UclFantasyApiException(
                UclFantasyFailureKind.Transient,
                $"The UCL Fantasy request to '{requestPath}' timed out.",
                innerException: exception);
        }
        catch (OperationCanceledException exception)
        {
            logger.LogWarning(
                exception,
                "UCL Fantasy request {RequestPath} timed out.",
                requestPath);
            throw new UclFantasyApiException(
                UclFantasyFailureKind.Transient,
                $"The UCL Fantasy request to '{requestPath}' timed out.",
                innerException: exception);
        }
        catch (BrokenCircuitException exception)
        {
            logger.LogWarning(
                exception,
                "UCL Fantasy request {RequestPath} was blocked by the open circuit.",
                requestPath);
            throw new UclFantasyApiException(
                UclFantasyFailureKind.Transient,
                $"The UCL Fantasy request to '{requestPath}' was blocked by the open circuit.",
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "UCL Fantasy request {RequestPath} failed after transient transport retries.",
                requestPath);
            throw new UclFantasyApiException(
                UclFantasyFailureKind.Transient,
                $"The UCL Fantasy request to '{requestPath}' failed.",
                exception.StatusCode,
                exception);
        }
    }

    private Uri BuildFixturesUri(UclFantasyWebConfigurationValue webConfiguration)
    {
        var feedBaseUri = GetFeedBaseUri(webConfiguration.FeedBaseUrl);
        var template = string.IsNullOrWhiteSpace(webConfiguration.FixturesUrl)
            ? options.FixturesPathTemplate
            : webConfiguration.FixturesUrl;
        var fixturePath = template
            .Replace(
                "{{tour_id}}",
                webConfiguration.TourId.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            .Replace(
                "{{tourId}}",
                webConfiguration.TourId.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            .Replace("{{lang}}", options.Language, StringComparison.Ordinal);

        return Uri.TryCreate(fixturePath, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri(feedBaseUri, fixturePath);
    }

    private Uri GetFeedBaseUri(string? configuredFeedBaseUrl)
    {
        if (Uri.TryCreate(configuredFeedBaseUrl, UriKind.Absolute, out var feedBaseUri))
        {
            return EnsureTrailingSlash(feedBaseUri);
        }

        var feedPath = options.FeedBasePath.TrimStart('/');
        var baseAddress = httpClient.BaseAddress ?? options.BaseAddress;
        return new Uri(baseAddress, feedPath);
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(uri.AbsoluteUri + "/", UriKind.Absolute);
    }

    private UclFantasyApiException CreateStatusException(
        string requestPath,
        HttpStatusCode statusCode)
    {
        var failureKind = IsTransient(statusCode)
            ? UclFantasyFailureKind.Transient
            : UclFantasyFailureKind.Permanent;

        logger.LogWarning(
            "UCL Fantasy request {RequestPath} failed with {FailureKind} HTTP status {StatusCode}.",
            requestPath,
            failureKind,
            (int)statusCode);

        return new UclFantasyApiException(
            failureKind,
            $"The UCL Fantasy request to '{requestPath}' failed with HTTP status {(int)statusCode}.",
            statusCode);
    }

    private UclFantasyApiException CreateInvalidPayloadException(
        string requestPath,
        Exception? innerException = null)
    {
        logger.LogWarning(
            innerException,
            "UCL Fantasy request {RequestPath} returned an invalid payload.",
            requestPath);

        return new UclFantasyApiException(
            UclFantasyFailureKind.InvalidPayload,
            $"The UCL Fantasy request to '{requestPath}' returned an invalid payload.",
            innerException: innerException);
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests ||
            (int)statusCode >= 500;
    }
}
