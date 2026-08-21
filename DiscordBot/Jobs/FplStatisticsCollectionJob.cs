using DiscordBot.FantasyPremierLeague.Historical;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.Jobs;

[DisallowConcurrentExecution]
public sealed class FplStatisticsCollectionJob(
    FplStatisticsCollectionService collectionService,
    TimeProvider timeProvider,
    ILogger<FplStatisticsCollectionJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        return JobExecutionLogging.RunAsync(
            context,
            timeProvider,
            logger,
            async execution =>
            {
                var snapshot = await collectionService.CollectLatestMissingGameweekAsync(
                    context.CancellationToken);
                if (snapshot is null)
                {
                    execution.SetEvent("NoNewFinishedGameweek");
                    return new JobExecutionResult("SkippedNoNewFinishedGameweek");
                }

                execution.SetEvent($"{snapshot.Season}-event-{snapshot.EventId}");
                return new JobExecutionResult("Completed");
            });
    }
}
