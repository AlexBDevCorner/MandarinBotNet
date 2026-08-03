using System.Reflection;
using AwesomeAssertions;
using DiscordBot.Jobs;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Quartz;

namespace DiscordBot.Tests.Jobs;

[TestFixture]
public sealed class JobExecutionLoggingTests
{
    [Test]
    public async Task RunAsync_CompletedJob_LogsStructuredStartAndOutcome()
    {
        // Arrange
        var logger = new RecordingLogger<TestJob>();
        var context = CreateContext(refireCount: 2);

        // Act
        await JobExecutionLogging.RunAsync(
            context,
            TimeProvider.System,
            logger,
            execution =>
            {
                execution.Attempt.Should().Be(3);
                execution.SetEvent("event-42");
                return Task.FromResult(new JobExecutionResult(
                    "CompletedWithDeliveryFailures",
                    DeliveredCount: 2,
                    SkippedCount: 1,
                    FailedCount: 1));
            });

        // Assert
        logger.Entries.Should().HaveCount(2);
        var started = logger.Entries[0];
        started.Level.Should().Be(LogLevel.Information);
        started.Properties["Job"].Should().Be("jobs.test-job");
        started.Properties["FireInstanceId"].Should().Be("fire-123");
        started.Properties["Attempt"].Should().Be(3);

        var finished = logger.Entries[1];
        finished.Properties["Event"].Should().Be("event-42");
        finished.Properties["Outcome"].Should().Be("CompletedWithDeliveryFailures");
        finished.Properties["DeliveredCount"].Should().Be(2);
        finished.Properties["SkippedCount"].Should().Be(1);
        finished.Properties["FailedCount"].Should().Be(1);
        finished.Properties.Should().ContainKey("DurationMs");
    }

    [Test]
    public async Task RunAsync_UnhandledFailure_LogsExceptionAndRethrows()
    {
        // Arrange
        var logger = new RecordingLogger<TestJob>();
        var context = CreateContext();
        var exception = new InvalidOperationException("FPL unavailable.");

        // Act
        Func<Task> act = () => JobExecutionLogging.RunAsync(
            context,
            TimeProvider.System,
            logger,
            _ => Task.FromException<JobExecutionResult>(exception));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("FPL unavailable.");
        var failure = logger.Entries.Single(entry => entry.Level == LogLevel.Error);
        failure.Exception.Should().BeSameAs(exception);
        failure.Properties["Outcome"].Should().Be("Failed");
        failure.Properties.Should().ContainKey("DurationMs");
    }

    private static IJobExecutionContext CreateContext(int refireCount = 0)
    {
        var context = DispatchProxy.Create<
            IJobExecutionContext,
            JobExecutionContextProxy>();
        var proxy = (JobExecutionContextProxy)(object)context;
        proxy.JobDetail = JobBuilder
            .Create<TestJob>()
            .WithIdentity("test-job", "jobs")
            .Build();
        proxy.FireInstanceId = "fire-123";
        proxy.RefireCount = refireCount;
        return context;
    }

    public class JobExecutionContextProxy : DispatchProxy
    {
        public required IJobDetail JobDetail { get; set; }

        public required string FireInstanceId { get; set; }

        public int RefireCount { get; set; }

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_JobDetail" => JobDetail,
                "get_FireInstanceId" => FireInstanceId,
                "get_RefireCount" => RefireCount,
                "get_CancellationToken" => CancellationToken.None,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }
    }

    private sealed class TestJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
}
