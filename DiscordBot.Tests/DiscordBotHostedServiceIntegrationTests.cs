using AwesomeAssertions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DiscordBot.Tests;

[TestFixture]
public sealed class DiscordBotHostedServiceIntegrationTests
{
    [Test]
    public async Task StartAsync_GatewayBecomesReady_CompletesBeforeScheduledWorkCanStart()
    {
        // Arrange
        var settings = new DiscordBotSettings(
            Token: "test-token",
            ReadinessTimeout: TimeSpan.FromSeconds(5));
        var readiness = new DiscordConnectionReadiness(settings);
        var gateway = new TestDiscordGatewayConnection();
        using var client = new DiscordSocketClient();
        var scheduledWork = new StartTrackingHostedService();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(client);
        builder.Services.AddSingleton(readiness);
        builder.Services.AddSingleton<IDiscordGatewayConnection>(gateway);
        builder.Services.AddHostedService<DiscordBotHostedService>();
        builder.Services.AddSingleton<IHostedService>(scheduledWork);
        using var host = builder.Build();

        // Act
        var startTask = host.StartAsync();
        await gateway.StartCalled.Task;

        // Assert
        startTask.IsCompleted.Should().BeFalse();
        scheduledWork.Started.Task.IsCompleted.Should().BeFalse();
        readiness.IsReady.Should().BeFalse();
        gateway.Operations.Should().Equal("Login", "Start");

        await gateway.RaiseReadyAsync();
        await startTask;
        scheduledWork.Started.Task.IsCompletedSuccessfully.Should().BeTrue();
        readiness.IsReady.Should().BeTrue();

        await gateway.RaiseDisconnectedAsync();
        readiness.IsReady.Should().BeFalse();
        var reconnectWait = readiness.WaitUntilReadyAsync(CancellationToken.None);
        reconnectWait.IsCompleted.Should().BeFalse();

        await gateway.RaiseReadyAsync();
        await reconnectWait;
        readiness.IsReady.Should().BeTrue();

        await host.StopAsync();
        readiness.IsReady.Should().BeFalse();
        gateway.Operations.Should().Equal("Login", "Start", "Stop");
    }

    private sealed class StartTrackingHostedService : IHostedService
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started.SetResult();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestDiscordGatewayConnection : IDiscordGatewayConnection
    {
        public event Func<Task>? Ready;

        public event Func<Exception, Task>? Disconnected;

        public List<string> Operations { get; } = [];

        public TaskCompletionSource StartCalled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task LoginAsync(string token)
        {
            Operations.Add("Login");
            return Task.CompletedTask;
        }

        public Task StartAsync()
        {
            Operations.Add("Start");
            StartCalled.SetResult();
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            Operations.Add("Stop");
            return Task.CompletedTask;
        }

        public Task RaiseReadyAsync()
        {
            return Ready?.Invoke() ?? Task.CompletedTask;
        }

        public Task RaiseDisconnectedAsync()
        {
            return Disconnected?.Invoke(new InvalidOperationException("Gateway disconnected."))
                ?? Task.CompletedTask;
        }
    }
}
