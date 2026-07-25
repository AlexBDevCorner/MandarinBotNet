namespace DiscordBot;

public interface IDiscordConnectionReadiness
{
    bool IsReady { get; }

    Task WaitUntilReadyAsync(CancellationToken cancellationToken);
}

public sealed class DiscordConnectionReadiness : IDiscordConnectionReadiness
{
    private readonly object _sync = new();
    private readonly TimeSpan _timeout;
    private TaskCompletionSource _ready = CreateCompletionSource();

    public DiscordConnectionReadiness(DiscordBotSettings settings)
    {
        if (settings.ReadinessTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.ReadinessTimeout,
                "Discord readiness timeout must be greater than zero.");
        }

        _timeout = settings.ReadinessTimeout;
    }

    public bool IsReady
    {
        get
        {
            lock (_sync)
            {
                return _ready.Task.IsCompletedSuccessfully;
            }
        }
    }

    public async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        Task readyTask;

        lock (_sync)
        {
            readyTask = _ready.Task;
        }

        if (readyTask.IsCompletedSuccessfully)
        {
            return;
        }

        using var timeoutSource = new CancellationTokenSource(_timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        try
        {
            await readyTask.WaitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested &&
            timeoutSource.IsCancellationRequested)
        {
            throw new DiscordReadinessTimeoutException(_timeout);
        }
    }

    public void MarkReady()
    {
        lock (_sync)
        {
            _ready.TrySetResult();
        }
    }

    public void MarkDisconnected()
    {
        lock (_sync)
        {
            if (_ready.Task.IsCompleted)
            {
                _ready = CreateCompletionSource();
            }
        }
    }

    private static TaskCompletionSource CreateCompletionSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

public sealed class DiscordReadinessTimeoutException(TimeSpan timeout)
    : TimeoutException(
        $"Discord did not become ready within {timeout}. Discord-dependent work remains retryable.")
{
    public TimeSpan Timeout { get; } = timeout;
}
