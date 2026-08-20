using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.Jobs;

public sealed class JobExecutionLogContext(int attempt)
{
    public int Attempt { get; } = attempt;

    public string Event { get; private set; } = "NotSelected";

    public void SetEvent(string eventIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventIdentifier);
        Event = eventIdentifier;
    }
}

public readonly record struct JobExecutionResult(
    string Outcome,
    int DeliveredCount = 0,
    int SkippedCount = 0,
    int FailedCount = 0);

public static class JobExecutionLogging
{
    public static async Task RunAsync<TJob>(
        IJobExecutionContext context,
        TimeProvider timeProvider,
        ILogger<TJob> logger,
        Func<JobExecutionLogContext, Task<JobExecutionResult>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(executeAsync);

        var job = context.JobDetail.Key.ToString();
        var attempt = context.RefireCount + 1;
        var startedTimestamp = timeProvider.GetTimestamp();
        var logContext = new JobExecutionLogContext(attempt);

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["Job"] = job,
            ["FireInstanceId"] = context.FireInstanceId,
            ["Attempt"] = attempt
        });

        logger.LogInformation(
            "Job {Job} started for fire {FireInstanceId} on attempt {Attempt}",
            job,
            context.FireInstanceId,
            attempt);

        try
        {
            var result = await executeAsync(logContext);
            var durationMs = GetDurationMilliseconds(timeProvider, startedTimestamp);

            logger.LogInformation(
                "Job {Job} finished for event {Event} on attempt {Attempt} with outcome {Outcome}; " +
                "delivered {DeliveredCount}, skipped {SkippedCount}, failed {FailedCount} in {DurationMs} ms",
                job,
                logContext.Event,
                attempt,
                result.Outcome,
                result.DeliveredCount,
                result.SkippedCount,
                result.FailedCount,
                durationMs);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Job {Job} canceled for event {Event} on attempt {Attempt} after {DurationMs} ms with outcome {Outcome}",
                job,
                logContext.Event,
                attempt,
                GetDurationMilliseconds(timeProvider, startedTimestamp),
                "Canceled");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Job {Job} failed for event {Event} on attempt {Attempt} after {DurationMs} ms with outcome {Outcome}",
                job,
                logContext.Event,
                attempt,
                GetDurationMilliseconds(timeProvider, startedTimestamp),
                "Failed");
            throw;
        }
    }

    private static double GetDurationMilliseconds(
        TimeProvider timeProvider,
        long startedTimestamp)
    {
        return Math.Round(
            timeProvider.GetElapsedTime(startedTimestamp).TotalMilliseconds,
            2);
    }
}
