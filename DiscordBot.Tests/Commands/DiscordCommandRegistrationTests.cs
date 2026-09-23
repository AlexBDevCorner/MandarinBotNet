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
        commandClient.Commands.Should().HaveCount(12);
        commandClient.Commands!.Select(command => command.Name.Value).Should()
            .Equal(
                DiscordApplicationCommands.HugMeName,
                DiscordApplicationCommands.StandingsName,
                DiscordApplicationCommands.DeadlineName,
                DiscordApplicationCommands.BenchLeagueName,
                DiscordApplicationCommands.LiveInsightsName,
                DiscordApplicationCommands.ProfileName,
                DiscordApplicationCommands.AchievementsName,
                DiscordApplicationCommands.ChipsName,
                DiscordApplicationCommands.PricesName,
                DiscordApplicationCommands.ChipWatchName,
                DiscordApplicationCommands.EventWatchName,
                DiscordApplicationCommands.HelpName);
        commandClient.Commands.Cast<SlashCommandProperties>()
            .Select(command => command.Description.Value)
            .Should().Equal(
                "Обнимает вас! 🤗",
                "Показывает текущие таблицы лиг FPL. 🏆",
                "Показывает ближайшие дедлайны FPL и ЛЧ по рижскому времени. ⏰",
                "Показывает таблицу Лиги обогревателей скамейки. 🔥",
                "Показывает результаты лиги FPL в реальном времени. ⚡",
                "Показывает профиль и достижения менеджера FPL. 🏅",
                "Показывает лидеров сезона по достижениям FPL. 🎖️",
                "Объясняет фишки FPL и подсказывает, когда их использовать. 🃏",
                "Показывает изменения цен в составах нашей лиги FPL. 💰",
                "Проверяет доступные фишки FPL и ищет хорошие моменты для их использования. 🧠",
                "Проверяет мониторинг билетов Riga FC. 🎟️",
                "Показывает команды, лиги и возможности бота. 🤖");
    }

    [Test]
    public async Task SynchronizeAsync_ProfileCommand_HasRequiredManagerOption()
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
        var profileCommand = commandClient.Commands!.Cast<SlashCommandProperties>()
            .Single(command => command.Name.Value == DiscordApplicationCommands.ProfileName);
        profileCommand.Options.IsSpecified.Should().BeTrue();
        var option = profileCommand.Options.Value.Single();
        option.Name.Should().Be("manager");
        option.Type.Should().Be(ApplicationCommandOptionType.String);
        option.IsRequired.Should().BeTrue();
    }

    [Test]
    public async Task SynchronizeAsync_StandingsCommand_HasLeagueTopAndAroundOptions()
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
        var standings = commandClient.Commands!.Cast<SlashCommandProperties>()
            .Single(command => command.Name.Value == DiscordApplicationCommands.StandingsName);
        standings.Options.IsSpecified.Should().BeTrue();
        var options = standings.Options.Value;

        var league = options.Single(option => option.Name == "league");
        league.Type.Should().Be(ApplicationCommandOptionType.String);
        league.IsRequired.Should().NotBeTrue();
        league.Choices.Should().NotBeNull();
        league.Choices!.Select(choice => choice.Name).Should()
            .Equal("classic", "h2h");

        var top = options.Single(option => option.Name == "top");
        top.Type.Should().Be(ApplicationCommandOptionType.Integer);
        top.IsRequired.Should().NotBeTrue();
        top.MinValue.Should().Be(1d);
        top.MaxValue.Should().Be(20d);

        var around = options.Single(option => option.Name == "around");
        around.Type.Should().Be(ApplicationCommandOptionType.String);
        around.IsRequired.Should().NotBeTrue();
    }

    [Test]
    public async Task SynchronizeAsync_BenchLeagueCommand_HasGwAndTeamOptions()
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
        var benchLeague = commandClient.Commands!.Cast<SlashCommandProperties>()
            .Single(command => command.Name.Value == DiscordApplicationCommands.BenchLeagueName);
        benchLeague.Options.IsSpecified.Should().BeTrue();
        var options = benchLeague.Options.Value;

        var gw = options.Single(option => option.Name == "gw");
        gw.Type.Should().Be(ApplicationCommandOptionType.Integer);
        gw.IsRequired.Should().NotBeTrue();
        gw.MinValue.Should().Be(1d);
        gw.MaxValue.Should().Be(38d);

        var team = options.Single(option => option.Name == "team");
        team.Type.Should().Be(ApplicationCommandOptionType.String);
        team.IsRequired.Should().NotBeTrue();
    }

    [Test]
    public async Task SynchronizeAsync_EventWatchCommand_HasStatusAndTestSubcommands()
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
        var eventWatch = commandClient.Commands!.Cast<SlashCommandProperties>()
            .Single(command => command.Name.Value == DiscordApplicationCommands.EventWatchName);
        eventWatch.Options.IsSpecified.Should().BeTrue();
        var options = eventWatch.Options.Value;
        options.Select(option => option.Name).Should()
            .Equal(
                DiscordApplicationCommands.EventWatchStatusSubcommand,
                DiscordApplicationCommands.EventWatchTestSubcommand);
        options.Should().OnlyContain(option =>
            option.Type == ApplicationCommandOptionType.SubCommand);
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
        commandClient.Commands.Should().HaveCount(12);
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
