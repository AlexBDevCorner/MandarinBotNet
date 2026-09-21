using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.Jobs;

[DisallowConcurrentExecution]
public sealed class AutonomousWorkDispatcherJob(
    IAutonomousWorkDispatcherClient dispatcherClient,
    TimeProvider timeProvider,
    ILogger<AutonomousWorkDispatcherJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        return JobExecutionLogging.RunAsync(
            context,
            timeProvider,
            logger,
            async execution =>
            {
                execution.SetEvent("AutonomousWorkDispatch");
                await dispatcherClient.TriggerDispatcherAsync(context.CancellationToken);
                return new JobExecutionResult("Completed");
            });
    }
}
