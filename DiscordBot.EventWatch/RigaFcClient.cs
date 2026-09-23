using System.Net;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace DiscordBot.EventWatch;

public sealed class RigaFcClient(
    HttpClient httpClient,
    ILogger<RigaFcClient> logger)
{
    /// <summary>
    /// Public ticket landing page. Documented as the entry point for supporters,
    /// but never used as detection evidence: it renders its products dynamically.
    /// </summary>
    public static readonly Uri TicketLandingUri = new("https://rigafc.lv/biletes/");

    /// <summary>
    /// Authoritative Riga FC ticket catalogue (shop).
    /// </summary>
    public static readonly Uri TicketCatalogueUri = new("https://shop.rigafc.lv/tickets");

    /// <summary>
    /// Stable HTTP JSON source exposing the same Riga FC ticket products listed
    /// by the shop catalogue: Biļešu Serviss events for promoter
    /// "Riga Football Club biedrība". The shop page itself renders these
    /// products through a JavaScript widget, so detection reads this endpoint
    /// instead of scraping homepage, calendar, or news pages.
    /// </summary>
    public static readonly Uri TicketCatalogueApiUri = new(
        "https://www.bilesuserviss.lv/api/v1/events?language=lv&pageSize=50&promoters=21-69-7648");

    public static IReadOnlyList<Uri> PageUris { get; } =
    [
        TicketCatalogueApiUri
    ];

    public Task<string> GetTicketCatalogueAsync(CancellationToken cancellationToken) =>
        GetPageAsync(TicketCatalogueApiUri, cancellationToken);

    public async Task<string> GetPageAsync(
        Uri pageUri,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pageUri);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, pageUri);
            request.Headers.Accept.ParseAdd("application/json");
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

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new RigaFcApiException(
                    $"The Riga FC ticket catalogue request to '{pageUri}' returned an empty response.",
                    response.StatusCode);
            }

            return payload;
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
                "Riga FC ticket catalogue request {PageUri} exhausted its timeout retries.",
                pageUri);
            throw new RigaFcApiException(
                $"The Riga FC ticket catalogue request to '{pageUri}' timed out.",
                innerException: exception);
        }
        catch (OperationCanceledException exception)
        {
            logger.LogWarning(
                exception,
                "Riga FC ticket catalogue request {PageUri} timed out.",
                pageUri);
            throw new RigaFcApiException(
                $"The Riga FC ticket catalogue request to '{pageUri}' timed out.",
                innerException: exception);
        }
        catch (BrokenCircuitException exception)
        {
            logger.LogWarning(
                exception,
                "Riga FC ticket catalogue request {PageUri} was blocked by the open circuit.",
                pageUri);
            throw new RigaFcApiException(
                $"The Riga FC ticket catalogue request to '{pageUri}' was blocked by the open circuit.",
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Riga FC ticket catalogue request {PageUri} failed after transient transport retries.",
                pageUri);
            throw new RigaFcApiException(
                $"The Riga FC ticket catalogue request to '{pageUri}' failed.",
                exception.StatusCode,
                exception);
        }
    }

    private RigaFcApiException CreateStatusException(
        Uri pageUri,
        HttpStatusCode statusCode)
    {
        logger.LogWarning(
            "Riga FC ticket catalogue request {PageUri} failed with HTTP status {StatusCode}.",
            pageUri,
            (int)statusCode);

        return new RigaFcApiException(
            $"The Riga FC ticket catalogue request to '{pageUri}' failed with HTTP status {(int)statusCode}.",
            statusCode);
    }
}
