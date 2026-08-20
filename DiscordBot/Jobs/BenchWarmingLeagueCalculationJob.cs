using System.Globalization;
using DiscordBot.BenchWarming;
using DiscordBot.Notifications;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DiscordBot.Jobs;

[DisallowConcurrentExecution]
public sealed class BenchWarmingLeagueCalculationJob(
    IDiscordConnectionReadiness discordReadiness,
    BenchWarmingLeagueCalculationService calculationService,
    BenchWarmingMessageComposer messageComposer,
    IDiscordNotificationPublisher notificationPublisher,
    NotificationOptions notificationOptions,
    TimeProvider timeProvider,
    ILogger<BenchWarmingLeagueCalculationJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        return JobExecutionLogging.RunAsync(
            context,
            timeProvider,
            logger,
            async execution =>
            {
                await discordReadiness.WaitUntilReadyAsync(context.CancellationToken);

                var round = await calculationService
                    .CalculateLatestFinishedRoundAsync(context.CancellationToken);
                if (round is null)
                {
                    execution.SetEvent("NoFinishedEvent");
                    return new JobExecutionResult("SkippedNoFinishedEvent");
                }

                var sourceIdentifier =
                    $"{round.Season}-event-{round.EventId.ToString(CultureInfo.InvariantCulture)}";
                execution.SetEvent(sourceIdentifier);
                if (round.WasAlreadyCalculated)
                {
                    return new JobExecutionResult("SkippedRoundAlreadyCalculated");
                }

                var message = messageComposer.ComposeRoundSummary(round);
                var deliveredCount = 0;
                var skippedCount = 0;
                var failedCount = 0;

                foreach (var target in notificationOptions.Targets)
                {
                    try
                    {
                        var delivered = await notificationPublisher.PublishOnceAsync(
                            target,
                            sourceIdentifier,
                            NotificationTypes.BenchWarmingStandings,
                            message,
                            context.CancellationToken);
                        if (delivered)
                        {
                            deliveredCount++;
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                    catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        failedCount++;
                        logger.LogError(
                            exception,
                            "Failed to publish notification {NotificationType} for event {Event} to Discord " +
                            "guild {GuildId}, channel {ChannelId}, on job attempt {Attempt} with outcome {Outcome}",
                            NotificationTypes.BenchWarmingStandings,
                            sourceIdentifier,
                            target.GuildId,
                            target.ChannelId,
                            execution.Attempt,
                            "Failed");
                    }
                }

                return new JobExecutionResult(
                    failedCount == 0 ? "Completed" : "CompletedWithDeliveryFailures",
                    deliveredCount,
                    skippedCount,
                    failedCount);
            });
    }
}
