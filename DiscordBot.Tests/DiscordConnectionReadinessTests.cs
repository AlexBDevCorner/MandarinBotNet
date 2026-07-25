using AwesomeAssertions;
using NUnit.Framework;

namespace DiscordBot.Tests;

[TestFixture]
public sealed class DiscordConnectionReadinessTests
{
    [Test]
    public async Task WaitUntilReadyAsync_InitiallyDisconnected_WaitsUntilReady()
    {
        // Arrange
        var readiness = CreateReadiness();

        // Act
        var waitTask = readiness.WaitUntilReadyAsync(CancellationToken.None);
        readiness.MarkReady();
        await waitTask;

        // Assert
        readiness.IsReady.Should().BeTrue();
    }

    [Test]
    public async Task WaitUntilReadyAsync_DisconnectedAfterReady_WaitsForReconnect()
    {
        // Arrange
        var readiness = CreateReadiness();
        readiness.MarkReady();
        readiness.MarkDisconnected();

        // Act
        var reconnectWait = readiness.WaitUntilReadyAsync(CancellationToken.None);
        readiness.MarkReady();
        await reconnectWait;

        // Assert
        readiness.IsReady.Should().BeTrue();
    }

    [Test]
    public async Task WaitUntilReadyAsync_ReadinessTimesOut_ThrowsRetryableTimeout()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(20);
        var readiness = CreateReadiness(timeout);

        // Act
        Func<Task> wait = () => readiness.WaitUntilReadyAsync(CancellationToken.None);

        // Assert
        var exception = await wait.Should().ThrowAsync<DiscordReadinessTimeoutException>();
        exception.Which.Timeout.Should().Be(timeout);
        readiness.IsReady.Should().BeFalse();
    }

    [Test]
    public async Task WaitUntilReadyAsync_HostShutdownRequested_PropagatesCancellation()
    {
        // Arrange
        var readiness = CreateReadiness();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        // Act
        Func<Task> wait = () => readiness.WaitUntilReadyAsync(cancellationSource.Token);

        // Assert
        await wait.Should().ThrowAsync<OperationCanceledException>();
    }

    private static DiscordConnectionReadiness CreateReadiness(TimeSpan? timeout = null)
    {
        return new DiscordConnectionReadiness(
            new DiscordBotSettings(
                Token: "test-token",
                ReadinessTimeout: timeout ?? TimeSpan.FromSeconds(5)));
    }
}
