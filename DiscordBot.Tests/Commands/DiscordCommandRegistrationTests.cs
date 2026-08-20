using AwesomeAssertions;
using Discord;
using DiscordBot.Commands;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DiscordBot.Tests.Commands;

[TestFixture]
public sealed class DiscordCommandRegistrationTests
{
    [Test]
    public async Task SynchronizeOnceAsync_RepeatedReadyEvents_SynchronizesOnlyOnce()
    {
        // Arrange
        var synchronizer = new TestCommandSynchronizer();
        var logger = new TestLogger<DiscordCommandRegistrationCoordinator>();
        var coordinator = new DiscordCommandRegistrationCoordinator(
            synchronizer,
            logger);

        // Act
        await coordinator.SynchronizeOnceAsync(CancellationToken.None);
        await coordinator.SynchronizeOnceAsync(CancellationToken.None);

        // Assert
        synchronizer.CallCount.Should().Be(1);
    }

    [Test]
    public async Task SynchronizeOnceAsync_FirstAttemptFails_DoesNotRetryOnReconnect()
    {
        // Arrange
        var synchronizer = new TestCommandSynchronizer
        {
            Exception = new InvalidOperationException("Registration failed.")
        };
        var logger = new TestLogger<DiscordCommandRegistrationCoordinator>();
        var coordinator = new DiscordCommandRegistrationCoordinator(
            synchronizer,
            logger);

        // Act
        await coordinator.SynchronizeOnceAsync(CancellationToken.None);
        await coordinator.SynchronizeOnceAsync(CancellationToken.None);

        // Assert
        synchronizer.CallCount.Should().Be(1);
        logger.Messages.Should().Contain(message =>
            message.Level == LogLevel.Error &&
            message.Text.Contains("will not be retried on gateway reconnect"));
    }

    [Test]
    public async Task SynchronizeAsync_GlobalMode_BulkOverwritesDesiredCommandSet()
    {
        // Arrange
        var commandClient = new TestApplicationCommandClient();
        var synchronizer = new DiscordCommandSynchronizer(
            new DiscordCommandRegistrationOptions(
                DiscordCommandRegistrationMode.Global,
                GuildId: null),
            commandClient,
            new TestLogger<DiscordCommandSynchronizer>());

        // Act
        await synchronizer.SynchronizeAsync(CancellationToken.None);

        // Assert
        commandClient.GlobalCalls.Should().Be(1);
        commandClient.GuildCalls.Should().Be(0);
        commandClient.Commands.Should().HaveCount(4);
        commandClient.Commands!.Select(command => command.Name.Value).Should()
            .Equal(
                DiscordApplicationCommands.HugMeName,
                DiscordApplicationCommands.StandingsName,
                DiscordApplicationCommands.DeadlineName,
                DiscordApplicationCommands.BenchLeagueName);
    }

    [Test]
    public async Task SynchronizeAsync_GuildMode_BulkOverwritesConfiguredGuild()
    {
        // Arrange
        const ulong guildId = 123456789;
        var commandClient = new TestApplicationCommandClient();
        var synchronizer = new DiscordCommandSynchronizer(
            new DiscordCommandRegistrationOptions(
                DiscordCommandRegistrationMode.Guild,
                guildId),
            commandClient,
            new TestLogger<DiscordCommandSynchronizer>());

        // Act
        await synchronizer.SynchronizeAsync(CancellationToken.None);

        // Assert
        commandClient.GlobalCalls.Should().Be(0);
        commandClient.GuildCalls.Should().Be(1);
        commandClient.GuildId.Should().Be(guildId);
        commandClient.Commands.Should().HaveCount(4);
    }

    [Test]
    public async Task SynchronizeAsync_DisabledMode_DoesNotCallDiscord()
    {
        // Arrange
        var commandClient = new TestApplicationCommandClient();
        var synchronizer = new DiscordCommandSynchronizer(
            new DiscordCommandRegistrationOptions(
                DiscordCommandRegistrationMode.Disabled,
                GuildId: null),
            commandClient,
            new TestLogger<DiscordCommandSynchronizer>());

        // Act
        await synchronizer.SynchronizeAsync(CancellationToken.None);

        // Assert
        commandClient.GlobalCalls.Should().Be(0);
        commandClient.GuildCalls.Should().Be(0);
    }

    [Test]
    public async Task SynchronizeAsync_ClientFails_LogsActionableTargetDetails()
    {
        // Arrange
        var commandClient = new TestApplicationCommandClient
        {
            Exception = new InvalidOperationException("Network unavailable.")
        };
        var logger = new TestLogger<DiscordCommandSynchronizer>();
        var synchronizer = new DiscordCommandSynchronizer(
            new DiscordCommandRegistrationOptions(
                DiscordCommandRegistrationMode.Global,
                GuildId: null),
            commandClient,
            logger);

        // Act
        var act = () => synchronizer.SynchronizeAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Network unavailable.");
        logger.Messages.Should().Contain(message =>
            message.Level == LogLevel.Error &&
            message.Text.Contains("global") &&
            message.Text.Contains("Verify bot credentials"));
    }

    private sealed class TestCommandSynchronizer : IDiscordCommandSynchronizer
    {
        public int CallCount { get; private set; }

        public Exception? Exception { get; init; }

        public Task SynchronizeAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Exception is null
                ? Task.CompletedTask
                : Task.FromException(Exception);
        }
    }

    private sealed class TestApplicationCommandClient
        : IDiscordApplicationCommandClient
    {
        public int GlobalCalls { get; private set; }

        public int GuildCalls { get; private set; }

        public ulong? GuildId { get; private set; }

        public ApplicationCommandProperties[]? Commands { get; private set; }

        public Exception? Exception { get; init; }

        public Task BulkOverwriteGlobalCommandsAsync(
            ApplicationCommandProperties[] commands,
            CancellationToken cancellationToken)
        {
            GlobalCalls++;
            Commands = commands;
            return Complete();
        }

        public Task BulkOverwriteGuildCommandsAsync(
            ApplicationCommandProperties[] commands,
            ulong guildId,
            CancellationToken cancellationToken)
        {
            GuildCalls++;
            GuildId = guildId;
            Commands = commands;
            return Complete();
        }

        private Task Complete()
        {
            return Exception is null
                ? Task.CompletedTask
                : Task.FromException(Exception);
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Text)> Messages { get; } = [];

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
            Messages.Add((logLevel, formatter(state, exception)));
        }
    }
}
