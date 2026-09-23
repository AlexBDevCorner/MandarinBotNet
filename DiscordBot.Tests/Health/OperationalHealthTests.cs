using System.Text.Json;
using AwesomeAssertions;
using DiscordBot.Health;
using DiscordBot.Notifications;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NUnit.Framework;

namespace DiscordBot.Tests.Health;

[TestFixture]
public sealed class OperationalHealthTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"mandarinbot-health-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_temporaryDirectory, recursive: true);
    }

    [Test]
    public async Task CheckHealthAsync_DiscordIsDisconnected_ReturnsUnhealthy()
    {
        // Arrange
        var readiness = CreateReadiness();
        var check = new DiscordReadinessHealthCheck(readiness);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Test]
    public async Task CheckHealthAsync_DiscordIsReady_ReturnsHealthy()
    {
        // Arrange
        var readiness = CreateReadiness();
        readiness.MarkReady();
        var check = new DiscordReadinessHealthCheck(readiness);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Test]
    public async Task CheckHealthAsync_DiscordDisconnectsAfterReady_ReturnsUnhealthy()
    {
        // Arrange
        var readiness = CreateReadiness();
        readiness.MarkReady();
        readiness.MarkDisconnected();
        var check = new DiscordReadinessHealthCheck(readiness);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Test]
    public async Task CheckHealthAsync_InitializedSqliteStorage_ReturnsHealthy()
    {
        // Arrange
        var databasePath = Path.Combine(_temporaryDirectory, "notification-state.db");
        _ = new SqliteNotificationCheckpointStore(databasePath);
        var check = new SqliteStorageHealthCheck(databasePath);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Test]
    public async Task CheckHealthAsync_MissingSqliteStorage_ReturnsUnhealthy()
    {
        // Arrange
        var databasePath = Path.Combine(_temporaryDirectory, "missing.db");
        var check = new SqliteStorageHealthCheck(databasePath);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Test]
    public async Task PublishAsync_HealthyReport_WritesProbeableMachineState()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(now);
        var healthStatePath = Path.Combine(_temporaryDirectory, "health-state.json");
        var publisher = new FileHealthCheckPublisher(healthStatePath, timeProvider);
        var report = CreateReport(HealthStatus.Healthy);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);
        var result = await HealthStateProbe.CheckReadinessAsync(
            healthStatePath,
            TimeSpan.FromSeconds(15),
            timeProvider);

        // Assert
        result.IsHealthy.Should().BeTrue();
        var json = await File.ReadAllTextAsync(healthStatePath);
        json.Should().Contain("\"discord_gateway\":\"Healthy\"");
    }

    [Test]
    public async Task CheckReadinessAsync_UnreadyState_ReturnsUnhealthy()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(now);
        var healthStatePath = Path.Combine(_temporaryDirectory, "health-state.json");
        await WriteStateAsync(healthStatePath, now, ready: false);

        // Act
        var result = await HealthStateProbe.CheckReadinessAsync(
            healthStatePath,
            TimeSpan.FromSeconds(15),
            timeProvider);

        // Assert
        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Contain("readiness");
    }

    [Test]
    public async Task CheckReadinessAsync_StaleState_ReturnsUnhealthy()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 8, 9, 9, 0, 30, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(now);
        var healthStatePath = Path.Combine(_temporaryDirectory, "health-state.json");
        await WriteStateAsync(healthStatePath, now.AddSeconds(-16), ready: true);

        // Act
        var result = await HealthStateProbe.CheckReadinessAsync(
            healthStatePath,
            TimeSpan.FromSeconds(15),
            timeProvider);

        // Assert
        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Contain("stale");
    }

    private static DiscordConnectionReadiness CreateReadiness()
    {
        return new DiscordConnectionReadiness(
            new DiscordOptions
            {
                Token = "test-token",
                ReadinessTimeout = TimeSpan.FromSeconds(5)
            });
    }

    private static HealthReport CreateReport(HealthStatus status)
    {
        var entry = new HealthReportEntry(
            status,
            description: null,
            duration: TimeSpan.Zero,
            exception: null,
            data: new Dictionary<string, object>(StringComparer.Ordinal),
            tags: ["ready"]);

        return new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["discord_gateway"] = entry
            },
            TimeSpan.Zero);
    }

    private static Task WriteStateAsync(
        string path,
        DateTimeOffset observedAtUtc,
        bool ready)
    {
        var state = new PublishedHealthState(
            1,
            observedAtUtc,
            Live: true,
            Ready: ready,
            Checks: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["discord_gateway"] = ready ? "Healthy" : "Unhealthy"
            });

        return File.WriteAllTextAsync(path, JsonSerializer.Serialize(state));
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
