using System.Collections.Concurrent;
using AwesomeAssertions;
using Discord;
using Discord.WebSocket;
using DiscordBot.Commands;
using DiscordBot.WelcomeMessages;
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
        var options = new DiscordOptions
        {
            Token = "test-token",
            ReadinessTimeout = TimeSpan.FromSeconds(5)
        };
        var readiness = new DiscordConnectionReadiness(options);
        var gateway = new TestDiscordGatewayConnection();
        var commandSynchronizer = new TestCommandSynchronizer();
        var scheduledWork = new StartTrackingHostedService();
        var logMessages = new ConcurrentQueue<string>();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new CollectingLoggerProvider(logMessages));
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(readiness);
        builder.Services.AddSingleton<IDiscordGatewayConnection>(gateway);
        builder.Services.AddSingleton<DiscordNetLogHandler>();
        builder.Services.AddSingleton<IDiscordCommandSynchronizer>(commandSynchronizer);
        builder.Services.AddSingleton<DiscordCommandRegistrationCoordinator>();
        builder.Services.AddSingleton<IDeadlineCommandHandler,
            TestDeadlineCommandHandler>();
        builder.Services.AddSingleton<IStandingsCommandHandler,
            TestStandingsCommandHandler>();
        builder.Services.AddSingleton<IBenchLeagueCommandHandler,
            TestBenchLeagueCommandHandler>();
        builder.Services.AddSingleton<ILiveInsightsCommandHandler,
            TestLiveInsightsCommandHandler>();
        builder.Services.AddSingleton<IWelcomeMessageHandler,
            TestWelcomeMessageHandler>();
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
        logMessages.Should().Contain(message =>
            message.Contains("Bot is connected and ready.") &&
            message.Contains("event Ready") &&
            message.Contains("outcome Ready"));
        commandSynchronizer.CallCount.Should().Be(1);

        await gateway.RaiseDisconnectedAsync();
        readiness.IsReady.Should().BeFalse();
        var reconnectWait = readiness.WaitUntilReadyAsync(CancellationToken.None);
        reconnectWait.IsCompleted.Should().BeFalse();

        await gateway.RaiseReadyAsync();
        await reconnectWait;
        readiness.IsReady.Should().BeTrue();
        commandSynchronizer.CallCount.Should().Be(1);
        logMessages.Should().Contain(message =>
            message.Contains("event Reconnected") &&
            message.Contains("outcome Ready"));

        await host.StopAsync();
        readiness.IsReady.Should().BeFalse();
        gateway.Operations.Should().Equal("Login", "Start", "Logout");
        gateway.SubscriberCount.Should().Be(0);
    }

    [Test]
    public async Task HandleSlashCommandAsync_StandingsCommand_RoutesToDedicatedHandler()
    {
        // Arrange
        var options = new DiscordOptions
        {
            Token = "test-token",
            ReadinessTimeout = TimeSpan.FromSeconds(5)
        };
        var readiness = new DiscordConnectionReadiness(options);
        var gateway = new TestDiscordGatewayConnection();
        using var provider = CreateServiceProvider(options, readiness, gateway);
        var service = provider.GetRequiredService<DiscordBotHostedService>();
        var handler = provider.GetRequiredService<IStandingsCommandHandler>();
        var interaction = new TestSlashCommandInteraction(
            DiscordApplicationCommands.StandingsName);
        var startTask = service.StartAsync(CancellationToken.None);
        await gateway.RaiseReadyAsync();
        await startTask;

        // Act
        await service.HandleSlashCommandAsync(interaction);

        // Assert
        ((TestStandingsCommandHandler)handler).Interaction.Should()
            .BeSameAs(interaction);

        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task HandleSlashCommandAsync_DeadlineCommand_RoutesToDedicatedHandler()
    {
        // Arrange
        var options = new DiscordOptions
        {
            Token = "test-token",
            ReadinessTimeout = TimeSpan.FromSeconds(5)
        };
        var readiness = new DiscordConnectionReadiness(options);
        var gateway = new TestDiscordGatewayConnection();
        using var provider = CreateServiceProvider(options, readiness, gateway);
        var service = provider.GetRequiredService<DiscordBotHostedService>();
        var handler = provider.GetRequiredService<IDeadlineCommandHandler>();
        var interaction = new TestSlashCommandInteraction(
            DiscordApplicationCommands.DeadlineName);
        var startTask = service.StartAsync(CancellationToken.None);
        await gateway.RaiseReadyAsync();
        await startTask;

        // Act
        await service.HandleSlashCommandAsync(interaction);

        // Assert
        ((TestDeadlineCommandHandler)handler).Interaction.Should()
            .BeSameAs(interaction);

        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task HandleSlashCommandAsync_BenchLeagueCommand_RoutesToDedicatedHandler()
    {
        // Arrange
        var options = new DiscordOptions
        {
            Token = "test-token",
            ReadinessTimeout = TimeSpan.FromSeconds(5)
        };
        var readiness = new DiscordConnectionReadiness(options);
        var gateway = new TestDiscordGatewayConnection();
        using var provider = CreateServiceProvider(options, readiness, gateway);
        var service = provider.GetRequiredService<DiscordBotHostedService>();
        var handler = provider.GetRequiredService<IBenchLeagueCommandHandler>();
        var interaction = new TestSlashCommandInteraction(
            DiscordApplicationCommands.BenchLeagueName);
        var startTask = service.StartAsync(CancellationToken.None);
        await gateway.RaiseReadyAsync();
        await startTask;

        // Act
        await service.HandleSlashCommandAsync(interaction);

        // Assert
        ((TestBenchLeagueCommandHandler)handler).Interaction.Should()
            .BeSameAs(interaction);

        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task HandleSlashCommandAsync_LiveInsightsCommand_RoutesToDedicatedHandler()
    {
        // Arrange
        var options = new DiscordOptions
        {
            Token = "test-token",
            ReadinessTimeout = TimeSpan.FromSeconds(5)
        };
        var readiness = new DiscordConnectionReadiness(options);
        var gateway = new TestDiscordGatewayConnection();
        using var provider = CreateServiceProvider(options, readiness, gateway);
        var service = provider.GetRequiredService<DiscordBotHostedService>();
        var handler = provider.GetRequiredService<ILiveInsightsCommandHandler>();
        var interaction = new TestSlashCommandInteraction(
            DiscordApplicationCommands.LiveInsightsName);
        var startTask = service.StartAsync(CancellationToken.None);
        await gateway.RaiseReadyAsync();
        await startTask;

        // Act
        await service.HandleSlashCommandAsync(interaction);

        // Assert
        ((TestLiveInsightsCommandHandler)handler).Interaction.Should()
            .BeSameAs(interaction);

        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task HandleSlashCommandAsync_UnknownCommand_RespondsInRussian()
    {
        // Arrange
        var options = new DiscordOptions
        {
            Token = "test-token",
            ReadinessTimeout = TimeSpan.FromSeconds(5)
        };
        var readiness = new DiscordConnectionReadiness(options);
        var gateway = new TestDiscordGatewayConnection();
        using var provider = CreateServiceProvider(options, readiness, gateway);
        var service = provider.GetRequiredService<DiscordBotHostedService>();
        var interaction = new TestSlashCommandInteraction("unknown");
        var startTask = service.StartAsync(CancellationToken.None);
        await gateway.RaiseReadyAsync();
        await startTask;

        // Act
        await service.HandleSlashCommandAsync(interaction);

        // Assert
        interaction.Messages.Should().Equal("🤷 Неизвестная команда.");

        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task StartAsync_MemberJoins_ForwardsToWelcomeMessageHandler()
    {
        // Arrange
        var options = new DiscordOptions
        {
            Token = "test-token",
            ReadinessTimeout = TimeSpan.FromSeconds(5)
        };
        var readiness = new DiscordConnectionReadiness(options);
        var gateway = new TestDiscordGatewayConnection();
        using var provider = CreateServiceProvider(options, readiness, gateway);
        var service = provider.GetRequiredService<DiscordBotHostedService>();
        var handler = (TestWelcomeMessageHandler)provider
            .GetRequiredService<IWelcomeMessageHandler>();
        var member = new DiscordGuildMember(10, 20, "<@20>");
        var startTask = service.StartAsync(CancellationToken.None);
        await gateway.RaiseReadyAsync();
        await startTask;

        // Act
        await gateway.RaiseUserJoinedAsync(member);

        // Assert
        handler.Members.Should().ContainSingle().Which.Should().Be(member);

        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task StopAsync_ShutdownIsCanceled_DetachesAllEventHandlers()
    {
        // Arrange
        var options = new DiscordOptions
        {
            Token = "test-token",
            ReadinessTimeout = TimeSpan.FromSeconds(5)
        };
        var readiness = new DiscordConnectionReadiness(options);
        var gateway = new TestDiscordGatewayConnection
        {
            LogoutTask = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously).Task
        };
        using var provider = CreateServiceProvider(options, readiness, gateway);
        var service = provider.GetRequiredService<DiscordBotHostedService>();
        var startTask = service.StartAsync(CancellationToken.None);
        await gateway.RaiseReadyAsync();
        await startTask;
        using var shutdown = new CancellationTokenSource();
        shutdown.Cancel();

        // Act
        Func<Task> stop = () => service.StopAsync(shutdown.Token);

        // Assert
        await stop.Should().ThrowAsync<OperationCanceledException>();
        readiness.IsReady.Should().BeFalse();
        gateway.Operations.Should().Equal("Login", "Start", "Logout");
        gateway.SubscriberCount.Should().Be(0);
    }

    [Test]
    public void AddDiscordGateway_RegistersContainerOwnedSocketClientFactory()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDiscordGateway();

        // Assert
        var descriptor = services.Single(service =>
            service.ServiceType == typeof(DiscordSocketClient));
        descriptor.ImplementationFactory.Should().NotBeNull();
        descriptor.ImplementationInstance.Should().BeNull();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    private static ServiceProvider CreateServiceProvider(
        DiscordOptions options,
        DiscordConnectionReadiness readiness,
        IDiscordGatewayConnection gateway)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(options);
        services.AddSingleton(readiness);
        services.AddSingleton(gateway);
        services.AddSingleton<DiscordNetLogHandler>();
        services.AddSingleton<IDiscordCommandSynchronizer, TestCommandSynchronizer>();
        services.AddSingleton<DiscordCommandRegistrationCoordinator>();
        services.AddSingleton<IDeadlineCommandHandler,
            TestDeadlineCommandHandler>();
        services.AddSingleton<IStandingsCommandHandler,
            TestStandingsCommandHandler>();
        services.AddSingleton<IBenchLeagueCommandHandler,
            TestBenchLeagueCommandHandler>();
        services.AddSingleton<ILiveInsightsCommandHandler,
            TestLiveInsightsCommandHandler>();
        services.AddSingleton<IWelcomeMessageHandler,
            TestWelcomeMessageHandler>();
        services.AddSingleton<DiscordBotHostedService>();

        return services.BuildServiceProvider();
    }

    private sealed class CollectingLoggerProvider(ConcurrentQueue<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new CollectingLogger(messages);
        }

        public void Dispose()
        {
        }
    }

    private sealed class CollectingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue(formatter(state, exception));
        }
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
        private Func<LogMessage, Task>? _log;
        private Func<Task>? _ready;
        private Func<Exception, Task>? _disconnected;
        private Func<SocketSlashCommand, Task>? _slashCommandExecuted;
        private Func<DiscordGuildMember, Task>? _userJoined;

        public event Func<LogMessage, Task>? Log
        {
            add => _log += value;
            remove => _log -= value;
        }

        public event Func<Task>? Ready
        {
            add => _ready += value;
            remove => _ready -= value;
        }

        public event Func<Exception, Task>? Disconnected
        {
            add => _disconnected += value;
            remove => _disconnected -= value;
        }

        public event Func<SocketSlashCommand, Task>? SlashCommandExecuted
        {
            add => _slashCommandExecuted += value;
            remove => _slashCommandExecuted -= value;
        }

        public event Func<DiscordGuildMember, Task>? UserJoined
        {
            add => _userJoined += value;
            remove => _userJoined -= value;
        }

        public List<string> Operations { get; } = [];

        public TaskCompletionSource StartCalled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task LogoutTask { get; init; } = Task.CompletedTask;

        public int SubscriberCount =>
            SubscriberCountFor(_log) +
            SubscriberCountFor(_ready) +
            SubscriberCountFor(_disconnected) +
            SubscriberCountFor(_slashCommandExecuted) +
            SubscriberCountFor(_userJoined);

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

        public Task LogoutAsync()
        {
            Operations.Add("Logout");
            return LogoutTask;
        }

        public Task RaiseReadyAsync()
        {
            return _ready?.Invoke() ?? Task.CompletedTask;
        }

        public Task RaiseDisconnectedAsync()
        {
            return _disconnected?.Invoke(new InvalidOperationException("Gateway disconnected."))
                ?? Task.CompletedTask;
        }

        public Task RaiseUserJoinedAsync(DiscordGuildMember member)
        {
            return _userJoined?.Invoke(member) ?? Task.CompletedTask;
        }

        private static int SubscriberCountFor(Delegate? handlers)
        {
            return handlers?.GetInvocationList().Length ?? 0;
        }
    }

    private sealed class TestCommandSynchronizer : IDiscordCommandSynchronizer
    {
        public int CallCount { get; private set; }

        public Task SynchronizeAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class TestStandingsCommandHandler : IStandingsCommandHandler
    {
        public IDiscordSlashCommandInteraction? Interaction { get; private set; }

        public Task HandleAsync(IDiscordSlashCommandInteraction interaction)
        {
            Interaction = interaction;
            return Task.CompletedTask;
        }
    }

    private sealed class TestDeadlineCommandHandler : IDeadlineCommandHandler
    {
        public IDiscordSlashCommandInteraction? Interaction { get; private set; }

        public Task HandleAsync(IDiscordSlashCommandInteraction interaction)
        {
            Interaction = interaction;
            return Task.CompletedTask;
        }
    }

    private sealed class TestBenchLeagueCommandHandler : IBenchLeagueCommandHandler
    {
        public IDiscordSlashCommandInteraction? Interaction { get; private set; }

        public Task HandleAsync(IDiscordSlashCommandInteraction interaction)
        {
            Interaction = interaction;
            return Task.CompletedTask;
        }
    }

    private sealed class TestLiveInsightsCommandHandler : ILiveInsightsCommandHandler
    {
        public IDiscordSlashCommandInteraction? Interaction { get; private set; }

        public Task HandleAsync(IDiscordSlashCommandInteraction interaction)
        {
            Interaction = interaction;
            return Task.CompletedTask;
        }
    }

    private sealed class TestWelcomeMessageHandler : IWelcomeMessageHandler
    {
        public List<DiscordGuildMember> Members { get; } = [];

        public Task HandleAsync(DiscordGuildMember member)
        {
            Members.Add(member);
            return Task.CompletedTask;
        }
    }

    private sealed class TestSlashCommandInteraction(string name)
        : IDiscordSlashCommandInteraction
    {
        public string Name { get; } = name;

        public string UserMention => "<@123>";

        public List<string> Messages { get; } = [];

        public Task RespondAsync(string content)
        {
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task DeferAsync() => Task.CompletedTask;

        public Task ModifyOriginalResponseAsync(string content)
        {
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task FollowupAsync(string content)
        {
            Messages.Add(content);
            return Task.CompletedTask;
        }
    }
}
