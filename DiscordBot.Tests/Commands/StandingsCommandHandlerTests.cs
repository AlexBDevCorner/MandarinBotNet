using System.Net;
using AwesomeAssertions;
using DiscordBot.Commands;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.PremierLeague;
using DiscordBot.Responses;
using DiscordBot.Tests.PremierLeague;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DiscordBot.Tests.Commands;

[TestFixture]
public sealed class StandingsCommandHandlerTests
{
    private static readonly FantasyPremierLeagueOptions Options = new()
    {
        ClassicLeagueId = 101,
        HeadToHeadLeagueId = 202
    };

    [Test]
    public async Task HandleAsync_BothLeaguesSucceed_DefersThenFetchesConcurrentlyAndRespondsInOrder()
    {
        // Arrange
        var operations = new List<string>();
        var classicCompletion = new TaskCompletionSource<ClassicStandingsResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var headToHeadCompletion =
            new TaskCompletionSource<HeadToHeadStandingsResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new TestFantasyPremierLeagueClient(operations)
        {
            ClassicTask = classicCompletion.Task,
            HeadToHeadTask = headToHeadCompletion.Task
        };
        var interaction = new TestSlashCommandInteraction(operations);
        var handler = CreateHandler(client);

        // Act
        var handling = handler.HandleAsync(interaction);
        await WaitUntilAsync(() => client.ClassicCallCount == 1 &&
            client.HeadToHeadCallCount == 1);

        // Assert
        operations.Should().Equal("Defer", "FetchClassic", "FetchHeadToHead");
        handling.IsCompleted.Should().BeFalse();
        client.ClassicLeagueId.Should().Be(Options.ClassicLeagueId);
        client.HeadToHeadLeagueId.Should().Be(Options.HeadToHeadLeagueId);

        headToHeadCompletion.SetResult(CreateHeadToHeadResponse(
            new HeadToHeadStanding { Rank = 2, EntryName = "H2H Team", Total = 7 }));
        classicCompletion.SetResult(CreateClassicResponse(
            new ClassicStanding { Rank = 1, EntryName = "Classic Team", Total = 99 }));
        await handling;

        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().StartWith("Classic league standings:");
        interaction.Messages[0].Should().Contain(":one: Classic Team 99");
        interaction.Messages[0].Should().Contain("Head-to-head league standings:");
        interaction.Messages[0].Should().Contain(":two: H2H Team 7");
        interaction.Messages[0].IndexOf("Classic league", StringComparison.Ordinal)
            .Should().BeLessThan(
                interaction.Messages[0].IndexOf(
                    "Head-to-head league",
                    StringComparison.Ordinal));
    }

    [Test]
    public async Task HandleAsync_ClassicLeagueFails_ReturnsHeadToHeadAndUnavailableWarning()
    {
        // Arrange
        var logger = new RecordingLogger<StandingsCommandHandler>();
        var client = new TestFantasyPremierLeagueClient([])
        {
            ClassicTask = Task.FromException<ClassicStandingsResponse>(
                CreateApiException(FantasyPremierLeagueFailureKind.Transient)),
            HeadToHeadTask = Task.FromResult(CreateHeadToHeadResponse(
                new HeadToHeadStanding
                {
                    Rank = 1,
                    EntryName = "Available Team",
                    Total = 12
                }))
        };
        var interaction = new TestSlashCommandInteraction([]);
        var handler = CreateHandler(client, logger);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Contain(
            "Classic league standings:\nThis league is currently unavailable.");
        interaction.Messages[0].Should().Contain(":one: Available Team 12");
        var logEntry = logger.Entries.Should().ContainSingle().Which;
        logEntry.Level.Should().Be(LogLevel.Warning);
        logEntry.Properties["LeagueType"].Should().Be("Classic");
        logEntry.Properties["FailureKind"].Should()
            .Be(FantasyPremierLeagueFailureKind.Transient);
        logEntry.Properties["StatusCode"].Should()
            .Be(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task HandleAsync_BothLeaguesFail_ReturnsRetryMessageAndLogsEachFailure()
    {
        // Arrange
        var logger = new RecordingLogger<StandingsCommandHandler>();
        var client = new TestFantasyPremierLeagueClient([])
        {
            ClassicTask = Task.FromException<ClassicStandingsResponse>(
                CreateApiException(FantasyPremierLeagueFailureKind.Permanent)),
            HeadToHeadTask = Task.FromException<HeadToHeadStandingsResponse>(
                new InvalidOperationException("Unexpected test failure."))
        };
        var interaction = new TestSlashCommandInteraction([]);
        var handler = CreateHandler(client, logger);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().Equal(
            "The FPL standings are unavailable right now. Please try again later.");
        interaction.FollowupCount.Should().Be(0);
        logger.Entries.Should().HaveCount(2);
        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("Permanent"));
        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Error && entry.Message.Contains("Unexpected"));
    }

    [Test]
    public async Task HandleAsync_EmptyStandings_RepresentsBothSectionsExplicitly()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient([])
        {
            ClassicTask = Task.FromResult(CreateClassicResponse()),
            HeadToHeadTask = Task.FromResult(CreateHeadToHeadResponse())
        };
        var interaction = new TestSlashCommandInteraction([]);
        var handler = CreateHandler(client);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Split("(No standings returned.)")
            .Should().HaveCount(3);
    }

    [Test]
    public async Task HandleAsync_LongUnsafeStandings_ChunksInOrderWithoutMentionsOrDroppedEntries()
    {
        // Arrange
        var classicStandings = Enumerable.Range(1, 55)
            .Select(rank => new ClassicStanding
            {
                Rank = rank,
                EntryName = $"@everyone Classic team {rank:D2} with a deliberately long name",
                Total = 1_000 - rank
            })
            .ToArray();
        var client = new TestFantasyPremierLeagueClient([])
        {
            ClassicTask = Task.FromResult(CreateClassicResponse(classicStandings)),
            HeadToHeadTask = Task.FromResult(CreateHeadToHeadResponse(
                new HeadToHeadStanding
                {
                    Rank = 1,
                    EntryName = "@here H2H team",
                    Total = 15
                }))
        };
        var interaction = new TestSlashCommandInteraction([]);
        var handler = CreateHandler(client);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().HaveCountGreaterThan(1);
        interaction.Messages.Should().OnlyContain(message => message.Length <= 2_000);
        interaction.Messages.Should().OnlyContain(message =>
            !message.Contains("@everyone") && !message.Contains("@here"));
        var combined = string.Join("\n", interaction.Messages);
        for (var rank = 1; rank <= classicStandings.Length; rank++)
        {
            combined.Should().Contain($"Classic team {rank:D2}");
        }

        combined.IndexOf("Classic team 01", StringComparison.Ordinal).Should()
            .BeLessThan(combined.IndexOf("Classic team 55", StringComparison.Ordinal));
        combined.IndexOf("Classic team 55", StringComparison.Ordinal).Should()
            .BeLessThan(combined.IndexOf("H2H team", StringComparison.Ordinal));
        interaction.FollowupCount.Should().Be(interaction.Messages.Count - 1);
    }

    private static StandingsCommandHandler CreateHandler(
        IFantasyPremierLeagueClient client,
        ILogger<StandingsCommandHandler>? logger = null)
    {
        return new StandingsCommandHandler(
            client,
            Options,
            new PremierLeagueMessageCompositionService(TestTimeZones.Riga()),
            logger ?? new RecordingLogger<StandingsCommandHandler>());
    }

    private static FantasyPremierLeagueApiException CreateApiException(
        FantasyPremierLeagueFailureKind kind)
    {
        return new FantasyPremierLeagueApiException(
            kind,
            "Classified test failure.",
            HttpStatusCode.ServiceUnavailable);
    }

    private static ClassicStandingsResponse CreateClassicResponse(
        params ClassicStanding[] results)
    {
        return new ClassicStandingsResponse
        {
            Standings = new ClassicStandings { Results = [.. results] }
        };
    }

    private static HeadToHeadStandingsResponse CreateHeadToHeadResponse(
        params HeadToHeadStanding[] results)
    {
        return new HeadToHeadStandingsResponse
        {
            HeadToHeadStandings = new HeadToHeadStandings
            {
                Results = [.. results]
            }
        };
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class TestFantasyPremierLeagueClient(List<string> operations)
        : IFantasyPremierLeagueClient
    {
        public Task<ClassicStandingsResponse> ClassicTask { get; init; } =
            Task.FromResult(CreateClassicResponse());

        public Task<HeadToHeadStandingsResponse> HeadToHeadTask { get; init; } =
            Task.FromResult(CreateHeadToHeadResponse());

        public int ClassicCallCount { get; private set; }

        public int HeadToHeadCallCount { get; private set; }

        public int ClassicLeagueId { get; private set; }

        public int HeadToHeadLeagueId { get; private set; }

        public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ClassicStandingsResponse> GetClassicStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            operations.Add("FetchClassic");
            ClassicCallCount++;
            ClassicLeagueId = leagueId;
            return ClassicTask;
        }

        public Task<HeadToHeadStandingsResponse> GetHeadToHeadStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            operations.Add("FetchHeadToHead");
            HeadToHeadCallCount++;
            HeadToHeadLeagueId = leagueId;
            return HeadToHeadTask;
        }
    }

    private sealed class TestSlashCommandInteraction(List<string> operations)
        : IDiscordSlashCommandInteraction
    {
        public string Name => DiscordApplicationCommands.StandingsName;

        public string UserMention => "<@123>";

        public List<string> Messages { get; } = [];

        public int FollowupCount { get; private set; }

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
            FollowupCount++;
            Messages.Add(content);
            return Task.CompletedTask;
        }
    }
}
