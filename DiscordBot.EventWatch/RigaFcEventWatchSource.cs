using Microsoft.Extensions.Logging;

namespace DiscordBot.EventWatch;

public sealed class RigaFcEventWatchSource(
    RigaFcClient client,
    RigaFcTicketCatalogueParser parser,
    ILogger<RigaFcEventWatchSource> logger) : IEventWatchSource
{
    public string SourceName => "riga-fc-ticket-catalogue";

    public async Task<IReadOnlyList<EventWatchObservation>> CollectAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string payload;
        try
        {
            payload = await client.GetTicketCatalogueAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Riga FC ticket catalogue {SourceUrl} fetch failed with outcome {Outcome}.",
                RigaFcClient.TicketCatalogueApiUri,
                "Failed");
            throw;
        }

        try
        {
            var observations = parser.Parse(payload, RigaFcClient.TicketCatalogueApiUri);
            logger.LogInformation(
                "Riga FC ticket catalogue {SourceUrl} fetch succeeded with outcome {Outcome}; products {ObservationCount}.",
                RigaFcClient.TicketCatalogueApiUri,
                "Succeeded",
                observations.Count);
            return observations;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Riga FC ticket catalogue {SourceUrl} parse failed with outcome {Outcome}.",
                RigaFcClient.TicketCatalogueApiUri,
                "ParseFailed");
            throw;
        }
    }
}
