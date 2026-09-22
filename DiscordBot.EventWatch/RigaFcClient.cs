using System.Net;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace DiscordBot.EventWatch;

public sealed class RigaFcClient(
    HttpClient httpClient,
    ILogger<RigaFcClient> logger)
{
    public static readonly Uri HomepageUri = new("https://rigafc.lv/");

    public static readonly Uri CalendarUri = new("https://rigafc.lv/kalendars/");

    public static readonly Uri NewsUri = new("https://rigafc.lv/jaunumi/");

    public static IReadOnlyList<Uri> PageUris { get; } =
    [
        HomepageUri,
        CalendarUri,
        NewsUri
    ];

    public Task<string> GetHomepageAsync(CancellationToken cancellationToken) =>
        GetPageAsync(HomepageUri, cancellationToken);

    public Task<string> GetCalendarAsync(CancellationToken cancellationToken) =>
        GetPageAsync(CalendarUri, cancellationToken);

    public Task<string> GetNewsAsync(CancellationToken cancellationToken) =>
        GetPageAsync(NewsUri, cancellationToken);

    public async Task<string> GetPageAsync(
        Uri pageUri,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pageUri);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, pageUri);
            request.Headers.Accept.ParseAdd("text/html");
            if (!request.Headers.UserAgent.Any())
            {
                request.Headers.UserAgent.ParseAdd(RigaFcUserAgent.Value);
            }

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateStatusException(pageUri, response.StatusCode);
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(html))
            {
                throw new RigaFcApiException(
                    $"The Riga FC request to '{pageUri}' returned an empty page.",
                    response.StatusCode);
            }

            return html;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RigaFcApiException)
        {
            throw;
        }
        catch (TimeoutRejectedException exception)
        {
            logger.LogWarning(
                exception,
                "Riga FC request {PageUri} exhausted its timeout retries.",
                pageUri);
            throw new RigaFcApiException(
                $"The Riga FC request to '{pageUri}' timed out.",
                innerException: exception);
        }
        catch (OperationCanceledException exception)
        {
            logger.LogWarning(
                exception,
                "Riga FC request {PageUri} timed out.",
                pageUri);
            throw new RigaFcApiException(
                $"The Riga FC request to '{pageUri}' timed out.",
                innerException: exception);
        }
        catch (BrokenCircuitException exception)
        {
            logger.LogWarning(
                exception,
                "Riga FC request {PageUri} was blocked by the open circuit.",
                pageUri);
            throw new RigaFcApiException(
                $"The Riga FC request to '{pageUri}' was blocked by the open circuit.",
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Riga FC request {PageUri} failed after transient transport retries.",
                pageUri);
            throw new RigaFcApiException(
                $"The Riga FC request to '{pageUri}' failed.",
                exception.StatusCode,
                exception);
        }
    }

    private RigaFcApiException CreateStatusException(
        Uri pageUri,
        HttpStatusCode statusCode)
    {
        logger.LogWarning(
            "Riga FC request {PageUri} failed with HTTP status {StatusCode}.",
            pageUri,
            (int)statusCode);

        return new RigaFcApiException(
            $"The Riga FC request to '{pageUri}' failed with HTTP status {(int)statusCode}.",
            statusCode);
    }
}
