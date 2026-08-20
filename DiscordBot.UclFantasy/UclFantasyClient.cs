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
