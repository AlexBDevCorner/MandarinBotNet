using Microsoft.Extensions.Logging;

namespace DiscordBot.Commands;

public sealed class DiscordCommandRegistrationCoordinator(
    IDiscordCommandSynchronizer synchronizer,
    ILogger<DiscordCommandRegistrationCoordinator> logger)
{
    private int _synchronizationStarted;

    public async Task SynchronizeOnceAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _synchronizationStarted, 1) != 0)
        {
            logger.LogDebug(
                "Skipping Discord application-command synchronization because it was already attempted during this process startup.");
            return;
        }

        try
        {
            await synchronizer.SynchronizeAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Discord application-command synchronization failed. " +
                "It will not be retried on gateway reconnect; restart after correcting the reported error.");
        }
    }
}
