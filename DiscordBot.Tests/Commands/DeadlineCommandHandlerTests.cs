using System.Net;
using AwesomeAssertions;
using DiscordBot.Commands;
using DiscordBot.Deadlines;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.PremierLeague;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DiscordBot.Tests.Commands;

[TestFixture]
public sealed class DeadlineCommandHandlerTests
{
    [TestCase(
        "2027-02-02T12:00:00+00:00",
        "⏰ FPL — тур 42: вторник, 2 февраля 2027 в 14:00 (Рига, Латвия, UTC+02:00).")]
    [TestCase(
        "2027-08-02T12:00:00+00:00",
        "⏰ FPL — тур 42: понедельник, 2 августа 2027 в 15:00 (Рига, Латвия, UTC+03:00).")]
    public async Task HandleAsync_UpcomingDeadline_DisplaysRigaCivilTime(
        string deadlineText,
        string expectedMessage)
    {
        // Arrange
        var deadline = DateTimeOffset.Parse(deadlineText);
        var operations = new List<string>();
        var provider = new TestDeadlineProvider(operations)
        {
            Result = new CompetitionDeadline("FPL", "Gameweek", 42, deadline)
        };
        var interaction = new TestSlashCommandInteraction(operations);
        var handler = CreateHandler(provider);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        operations.Should().Equal("Defer", "Fetch", "Modify");
        interaction.Messages.Should().Equal(expectedMessage);
    }

    [Test]
    public async Task HandleAsync_FplAndUclDeadlines_DisplaysBothCompetitions()
    {
        // Arrange
        var operations = new List<string>();
        var fplProvider = new TestDeadlineProvider(operations)
        {
            Result = new CompetitionDeadline(
                "FPL",
                "Gameweek",
                42,
                new DateTimeOffset(2027, 2, 2, 12, 0, 0, TimeSpan.Zero))
        };
        var uclProvider = new TestDeadlineProvider(operations)
        {
            CompetitionName = "UCL",
            Result = new CompetitionDeadline(
                "UCL",
                "Matchday",
                1,
                new DateTimeOffset(2027, 2, 3, 12, 0, 0, TimeSpan.Zero))
        };
        var interaction = new TestSlashCommandInteraction(operations);
        var handler = CreateHandler(fplProvider, uclProvider);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        operations.Should().Equal("Defer", "Fetch", "Fetch", "Modify");
        interaction.Messages.Should().Equal(
            "⏰ FPL — тур 42: вторник, 2 февраля 2027 в 14:00 (Рига, Латвия, UTC+02:00).\n" +
            "⏰ UCL — игровой день 1: среда, 3 февраля 2027 в 14:00 (Рига, Латвия, UTC+02:00).");
    }

    [Test]
    public async Task HandleAsync_NoUpcomingDeadline_ReturnsHelpfulMessage()
    {
        // Arrange
        var provider = new TestDeadlineProvider([]);
        var interaction = new TestSlashCommandInteraction([]);
        var handler = CreateHandler(provider);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().Equal(
            "⏳ Информация о следующем дедлайне FPL пока не появилась.");
    }

    [Test]
    public async Task HandleAsync_FplApiFails_ReturnsRetryMessageAndLogsFailure()
    {
        // Arrange
        var logger = new RecordingLogger<DeadlineCommandHandler>();
        var provider = new TestDeadlineProvider([])
        {
            Exception = new FantasyPremierLeagueApiException(
                FantasyPremierLeagueFailureKind.Transient,
                "Classified test failure.",
                HttpStatusCode.ServiceUnavailable)
        };
        var interaction = new TestSlashCommandInteraction([]);
        var handler = CreateHandler(provider, logger);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().Equal(
            "⚠️ Дедлайн FPL сейчас недоступен. Попробуйте ещё раз позже.");
        var logEntry = logger.Entries.Should().ContainSingle().Which;
        logEntry.Level.Should().Be(LogLevel.Warning);
        logEntry.Properties["FailureKind"].Should()
            .Be(FantasyPremierLeagueFailureKind.Transient);
        logEntry.Properties["StatusCode"].Should()
            .Be(HttpStatusCode.ServiceUnavailable);
    }

    private static DeadlineCommandHandler CreateHandler(
        IUpcomingDeadlineProvider provider,
        ILogger<DeadlineCommandHandler>? logger = null)
    {
        return new DeadlineCommandHandler(
            [provider],
            DiscordBot.Tests.PremierLeague.TestTimeZones.Riga(),
            logger ?? new RecordingLogger<DeadlineCommandHandler>());
    }

    private static DeadlineCommandHandler CreateHandler(
        IUpcomingDeadlineProvider firstProvider,
        IUpcomingDeadlineProvider secondProvider)
    {
        return new DeadlineCommandHandler(
            [firstProvider, secondProvider],
            DiscordBot.Tests.PremierLeague.TestTimeZones.Riga(),
            new RecordingLogger<DeadlineCommandHandler>());
    }

    private sealed class TestDeadlineProvider(List<string> operations)
        : IUpcomingDeadlineProvider
    {
        public string CompetitionName { get; init; } = "FPL";

        public CompetitionDeadline? Result { get; init; }

        public Exception? Exception { get; init; }

        public Task<CompetitionDeadline?> GetNextAsync(
            CancellationToken cancellationToken)
        {
            operations.Add("Fetch");
            return Exception is null
                ? Task.FromResult(Result)
                : Task.FromException<CompetitionDeadline?>(Exception);
        }
    }

    private sealed class TestSlashCommandInteraction(List<string> operations)
        : IDiscordSlashCommandInteraction
    {
        public string Name => DiscordApplicationCommands.DeadlineName;

        public string UserMention => "<@123>";

        public string? GetStringOption(string name) => null;

        public List<string> Messages { get; } = [];

        public Task RespondAsync(string content)
        {
            operations.Add("Respond");
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task DeferAsync()
        {
            operations.Add("Defer");
            return Task.CompletedTask;
        }

        public Task ModifyOriginalResponseAsync(string content)
        {
            operations.Add("Modify");
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task FollowupAsync(string content)
        {
            operations.Add("Followup");
            Messages.Add(content);
            return Task.CompletedTask;
        }
    }
}
