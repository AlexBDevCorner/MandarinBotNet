using Microsoft.Extensions.Logging;

namespace DiscordBot.EventWatch;

public sealed class RigaFcEventWatchSource(
    RigaFcClient client,
    RigaFcPageParser parser,
    ILogger<RigaFcEventWatchSource> logger) : IEventWatchSource
{
    public string SourceName => "riga-fc";

    public async Task<IReadOnlyList<EventWatchObservation>> CollectAsync(
        CancellationToken cancellationToken)
    {
        var observations = new List<EventWatchObservation>();

        foreach (var pageUri in RigaFcClient.PageUris)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string html;
            try
            {
                html = await client.GetPageAsync(pageUri, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Riga FC source {SourceUrl} fetch failed with outcome {Outcome}.",
                    pageUri,
                    "Failed");
                continue;
            }

            IReadOnlyList<EventWatchObservation> pageObservations;
            try
            {
                pageObservations = await parser.ParseAsync(
                    pageUri,
                    html,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Riga FC source {SourceUrl} parse failed with outcome {Outcome}.",
                    pageUri,
                    "ParseFailed");
                continue;
            }

            logger.LogInformation(
                "Riga FC source {SourceUrl} fetch succeeded with outcome {Outcome}; observations {ObservationCount}.",
                pageUri,
                "Succeeded",
                pageObservations.Count);
            observations.AddRange(pageObservations);
        }

        return observations;
    }
}
