using AwesomeAssertions;
using DiscordBot.Notifications;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DiscordBot.Tests.Notifications
{
    [TestFixture]
    public sealed class NotificationDeliveryCoordinatorTests
    {
        private string _testDirectory = null!;
        private string _databasePath = null!;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(
                Path.GetTempPath(),
                "MandarinBotNet.Tests",
                Guid.NewGuid().ToString("N"));
            _databasePath = Path.Combine(_testDirectory, "notification-state.db");
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        [Test]
        public async Task SendOnceAsync_NewCoordinatorUsingSameDatabase_DoesNotSendAgain()
        {
            // Arrange
            var checkpoint = CreateCheckpoint();
            var firstCoordinator = CreateCoordinator();
            var sendCount = 0;

            // Act
            var firstResult = await firstCoordinator.SendOnceAsync(
                checkpoint,
                () =>
                {
                    sendCount++;
                    return Task.CompletedTask;
                });

            var restartedCoordinator = CreateCoordinator();
            var secondResult = await restartedCoordinator.SendOnceAsync(
                checkpoint,
                () =>
                {
                    sendCount++;
                    return Task.CompletedTask;
                });

            // Assert
            firstResult.Should().BeTrue();
            secondResult.Should().BeFalse();
            sendCount.Should().Be(1);
        }

        [Test]
        public async Task SendOnceAsync_SendFails_DoesNotCheckpointAndRemainsRetryable()
        {
            // Arrange
            var checkpoint = CreateCheckpoint();
            var coordinator = CreateCoordinator();
            var sendCount = 0;

            Func<Task> failedDelivery = () => coordinator.SendOnceAsync(
                checkpoint,
                () =>
                {
                    sendCount++;
                    throw new InvalidOperationException("Discord rejected the message.");
                });

            // Act
            await failedDelivery.Should().ThrowAsync<InvalidOperationException>();
            var retryResult = await coordinator.SendOnceAsync(
                checkpoint,
                () =>
                {
                    sendCount++;
                    return Task.CompletedTask;
                });

            // Assert
            retryResult.Should().BeTrue();
            sendCount.Should().Be(2);
            new SqliteNotificationCheckpointStore(_databasePath)
                .IsDelivered(checkpoint)
                .Should()
                .BeTrue();
        }

        [Test]
        public async Task SendOnceAsync_PartialChannelFailure_RetriesOnlyFailedChannelAfterRestart()
        {
            // Arrange
            var successfulCheckpoint = CreateCheckpoint(channelId: 100);
            var failedCheckpoint = CreateCheckpoint(channelId: 200);
            var coordinator = CreateCoordinator();
            var successfulSendCount = 0;
            var failedSendCount = 0;

            // Act
            await coordinator.SendOnceAsync(
                successfulCheckpoint,
                () =>
                {
                    successfulSendCount++;
                    return Task.CompletedTask;
                });

            Func<Task> failedDelivery = () => coordinator.SendOnceAsync(
                failedCheckpoint,
                () =>
                {
                    failedSendCount++;
                    throw new InvalidOperationException("Discord rejected the message.");
                });
            await failedDelivery.Should().ThrowAsync<InvalidOperationException>();

            var restartedCoordinator = CreateCoordinator();
            var successfulRetryResult = await restartedCoordinator.SendOnceAsync(
                successfulCheckpoint,
                () =>
                {
                    successfulSendCount++;
                    return Task.CompletedTask;
                });
            var failedRetryResult = await restartedCoordinator.SendOnceAsync(
                failedCheckpoint,
                () =>
                {
                    failedSendCount++;
                    return Task.CompletedTask;
                });

            // Assert
            successfulRetryResult.Should().BeFalse();
            failedRetryResult.Should().BeTrue();
            successfulSendCount.Should().Be(1);
            failedSendCount.Should().Be(2);
        }

        [Test]
        public async Task SendOnceAsync_ConcurrentAttempts_SendsOnlyOnce()
        {
            // Arrange
            var checkpoint = CreateCheckpoint();
            var coordinator = CreateCoordinator();
            var sendStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowSendToComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var sendCount = 0;

            async Task SendAsync()
            {
                Interlocked.Increment(ref sendCount);
                sendStarted.SetResult();
                await allowSendToComplete.Task;
            }

            // Act
            var firstAttempt = coordinator.SendOnceAsync(checkpoint, SendAsync);
            await sendStarted.Task;
            var secondAttempt = coordinator.SendOnceAsync(checkpoint, SendAsync);
            allowSendToComplete.SetResult();
            var results = await Task.WhenAll(firstAttempt, secondAttempt);

            // Assert
            results.Should().BeEquivalentTo([true, false], options => options.WithStrictOrdering());
            sendCount.Should().Be(1);
        }

        [Test]
        public async Task SendOnceAsync_Success_RecordsInjectedUtcTime()
        {
            // Arrange
            var utcNow = new DateTimeOffset(
                2027,
                2,
                2,
                12,
                0,
                0,
                TimeSpan.Zero);
            var store = new RecordingCheckpointStore();
            var coordinator = new NotificationDeliveryCoordinator(
                store,
                new FixedTimeProvider(utcNow),
                new RecordingLogger<NotificationDeliveryCoordinator>());

            // Act
            await coordinator.SendOnceAsync(
                CreateCheckpoint(),
                () => Task.CompletedTask);

            // Assert
            store.DeliveredAtUtc.Should().Be(utcNow);
        }

        [Test]
        public void RecordDelivered_DifferentCompositeKeyParts_RemainUndelivered()
        {
            // Arrange
            var store = new SqliteNotificationCheckpointStore(_databasePath);
            var checkpoint = CreateCheckpoint();

            // Act
            store.RecordDelivered(checkpoint, DateTimeOffset.UtcNow);

            // Assert
            store.IsDelivered(checkpoint with { GuildId = checkpoint.GuildId + 1 }).Should().BeFalse();
            store.IsDelivered(checkpoint with { ChannelId = checkpoint.ChannelId + 1 }).Should().BeFalse();
            store.IsDelivered(checkpoint with { SourceIdentifier = "event-43" }).Should().BeFalse();
            store.IsDelivered(checkpoint with { NotificationType = NotificationTypes.Deadline1Hour }).Should().BeFalse();
        }

        [Test]
        public async Task SendOnceAsync_FailureThenRetry_LogsDiagnosableAttemptsAndOutcomes()
        {
            // Arrange
            var checkpoint = CreateCheckpoint();
            var logger = new RecordingLogger<NotificationDeliveryCoordinator>();
            var coordinator = CreateCoordinator(logger);
            var exception = new InvalidOperationException("Discord rejected the message.");

            // Act
            Func<Task> failedDelivery = () => coordinator.SendOnceAsync(
                checkpoint,
                () => Task.FromException(exception));
            await failedDelivery.Should().ThrowAsync<InvalidOperationException>();
            await coordinator.SendOnceAsync(checkpoint, () => Task.CompletedTask);

            // Assert
            var failure = logger.Entries.Single(entry => entry.Level == LogLevel.Error);
            failure.Exception.Should().BeSameAs(exception);
            failure.Properties["GuildId"].Should().Be(checkpoint.GuildId);
            failure.Properties["ChannelId"].Should().Be(checkpoint.ChannelId);
            failure.Properties["Event"].Should().Be(checkpoint.SourceIdentifier);
            failure.Properties["NotificationType"].Should().Be(checkpoint.NotificationType);
            failure.Properties["Attempt"].Should().Be(1);
            failure.Properties["Outcome"].Should().Be("Failed");
            failure.Properties.Should().ContainKey("DurationMs");

            var success = logger.Entries.Single(entry =>
                entry.Properties.GetValueOrDefault("Outcome") as string ==
                "DeliveredAndCheckpointed");
            success.Properties["Attempt"].Should().Be(2);
        }

        [Test]
        public async Task SendOnceAsync_ExistingCheckpoint_LogsSkipDecision()
        {
            // Arrange
            var checkpoint = CreateCheckpoint();
            var store = new SqliteNotificationCheckpointStore(_databasePath);
            store.RecordDelivered(checkpoint, DateTimeOffset.UtcNow);
            var logger = new RecordingLogger<NotificationDeliveryCoordinator>();
            var coordinator = new NotificationDeliveryCoordinator(
                store,
                TimeProvider.System,
                logger);

            // Act
            var result = await coordinator.SendOnceAsync(
                checkpoint,
                () => Task.CompletedTask);

            // Assert
            result.Should().BeFalse();
            var decision = logger.Entries.Should().ContainSingle().Subject;
            decision.Properties["Outcome"].Should().Be("AlreadyDelivered");
            decision.Properties["GuildId"].Should().Be(checkpoint.GuildId);
            decision.Properties["ChannelId"].Should().Be(checkpoint.ChannelId);
        }

        private NotificationDeliveryCoordinator CreateCoordinator(
            ILogger<NotificationDeliveryCoordinator>? logger = null)
        {
            return new NotificationDeliveryCoordinator(
                new SqliteNotificationCheckpointStore(_databasePath),
                TimeProvider.System,
                logger ?? new RecordingLogger<NotificationDeliveryCoordinator>());
        }

        private static NotificationCheckpoint CreateCheckpoint(ulong channelId = 100)
        {
            return new NotificationCheckpoint(
                GuildId: 10,
                ChannelId: channelId,
                SourceIdentifier: "event-42",
                NotificationType: NotificationTypes.Deadline24Hours);
        }

        private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => utcNow;
        }

        private sealed class RecordingCheckpointStore : INotificationCheckpointStore
        {
            public DateTimeOffset? DeliveredAtUtc { get; private set; }

            public bool IsDelivered(NotificationCheckpoint checkpoint) => false;

            public void RecordDelivered(
                NotificationCheckpoint checkpoint,
                DateTimeOffset deliveredAtUtc)
            {
                DeliveredAtUtc = deliveredAtUtc;
            }
        }
    }
}
