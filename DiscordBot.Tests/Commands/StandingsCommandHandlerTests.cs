using System.Net;
using AwesomeAssertions;
using DiscordBot.Commands;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Standings;
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
    public async Task HandleAsync_NoOptions_FetchesBothLeaguesAndRendersRichRows()
    {
        // Arrange
        var operations = new List<string>();
        var client = new TestFantasyPremierLeagueClient(operations)
        {
            ClassicTask = Task.FromResult(CreateClassicResponse(
                new ClassicStanding
                {
                    Rank = 1,
                    Entry = 1,
                    EntryName = "Classic Team",
                    Total = 99,
                    EventTotal = 63
                })),
            HeadToHeadTask = Task.FromResult(CreateHeadToHeadResponse(
                new HeadToHeadStanding
                {
                    Rank = 1,
                    EntryName = "H2H Team",
                    Total = 12,
                    MatchesPlayed = 7
                }))
        };
        var interaction = new TestSlashCommandInteraction(operations);
        var handler = CreateHandler(client);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        client.ClassicCallCount.Should().Be(1);
        client.HeadToHeadCallCount.Should().Be(1);
        interaction.Messages.Should().ContainSingle();
        var message = interaction.Messages[0];
        message.Should().StartWith("🏆 Турнирная таблица классической лиги:");
        message.Should().Contain("🥇 Classic Team — 99 | GW 63");
        message.Should().Contain("⚔️ Турнирная таблица лиги один на один:");
        message.Should().Contain("🥇 H2H Team — 12 | 7 матчей");
        message.IndexOf("классической лиги", StringComparison.Ordinal)
            .Should().BeLessThan(
                message.IndexOf("лиги один на один", StringComparison.Ordinal));
    }

    [Test]
    public async Task HandleAsync_LeagueClassic_OnlyClassicRequested()
    {
        // Arrange
        var operations = new List<string>();
        var client = new TestFantasyPremierLeagueClient(operations)
        {
            ClassicTask = Task.FromResult(CreateClassicResponse(
                new ClassicStanding
                {
                    Rank = 1,
                    Entry = 1,
                    EntryName = "Classic Team",
                    Total = 99,
                    EventTotal = 63
                }))
        };
        var interaction = new TestSlashCommandInteraction(operations);
        interaction.SetStringOption("league", "classic");
        var handler = CreateHandler(client);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        client.ClassicCallCount.Should().Be(1);
        client.HeadToHeadCallCount.Should().Be(0);
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Contain("🥇 Classic Team — 99 | GW 63");
        interaction.Messages[0].Should().NotContain("один на один");
    }

    [Test]
    public async Task HandleAsync_LeagueH2H_OnlyHeadToHeadRequested()
    {
        // Arrange
        var operations = new List<string>();
        var client = new TestFantasyPremierLeagueClient(operations)
        {
            HeadToHeadTask = Task.FromResult(CreateHeadToHeadResponse(
                new HeadToHeadStanding
                {
                    Rank = 1,
                    EntryName = "H2H Team",
                    Total = 12,
                    MatchesPlayed = 7
                }))
        };
        var interaction = new TestSlashCommandInteraction(operations);
        interaction.SetStringOption("league", "h2h");
        var handler = CreateHandler(client);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        client.ClassicCallCount.Should().Be(0);
        client.HeadToHeadCallCount.Should().Be(1);
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Contain("🥇 H2H Team — 12 | 7 матчей");
        interaction.Messages[0].Should().NotContain("классической лиги");
    }

    [Test]
    public async Task HandleAsync_TopApplied_LimitsClassicRows()
    {
        // Arrange
        var operations = new List<string>();
        var classicStandings = Enumerable.Range(1, 10)
            .Select(rank => new ClassicStanding
            {
                Rank = rank,
                Entry = rank,
                EntryName = $"Team {rank}",
                Total = 110 - rank,
                EventTotal = 10
            })
            .ToArray();
        var client = new TestFantasyPremierLeagueClient(operations)
        {
            ClassicTask = Task.FromResult(CreateClassicResponse(classicStandings)),
            HeadToHeadTask = Task.FromResult(CreateHeadToHeadResponse(
                new HeadToHeadStanding
                {
                    Rank = 1,
                    EntryName = "H2H Team",
                    Total = 12,
                    MatchesPlayed = 7
                }))
        };
        var interaction = new TestSlashCommandInteraction(operations);
        interaction.SetIntegerOption("top", 5);
        var handler = CreateHandler(client);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        var message = interaction.Messages[0];
        message.Should().Contain("🥇 Team 1");
        message.Should().Contain("5. Team 5");
        message.Should().NotContain("6. Team 6");
        message.Should().Contain("H2H Team — 12 | 7 матчей");
    }

    [Test]
    public async Task HandleAsync_AroundClassic_OnlyClassicRequestedAndRendersWindow()
    {
        // Arrange
        var operations = new List<string>();
        var classicStandings = Enumerable.Range(1, 10)
            .Select(rank => new ClassicStanding
            {
                Rank = rank,
                Entry = rank,
                EntryName = $"Team {rank}",
                Total = 110 - rank,
                EventTotal = 10
            })
            .ToArray();
        var client = new TestFantasyPremierLeagueClient(operations)
        {
            ClassicTask = Task.FromResult(CreateClassicResponse(classicStandings))
        };
        var interaction = new TestSlashCommandInteraction(operations);
        interaction.SetStringOption("around", "Team 8");
        var handler = CreateHandler(client);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        client.ClassicCallCount.Should().Be(1);
        client.HeadToHeadCallCount.Should().Be(0);
        var message = interaction.Messages[0];
        message.Should().StartWith("🏆 Классическая лига — вокруг Team 8");
        message.Should().Contain("👉 8. Team 8");
        message.Should().Contain("6. Team 6");
        message.Should().Contain("10. Team 10");
        message.Should().NotContain("5. Team 5");
    }

    [Test]
    public async Task HandleAsync_AroundNotFound_ReturnsNotFoundMessage()
    {
        // Arrange
        var operations = new List<string>();
        var client = new TestFantasyPremierLeagueClient(operations)
        {
            ClassicTask = Task.FromResult(CreateClassicResponse(
                new ClassicStanding
                {
                    Rank = 1,
                    Entry = 1,
                    EntryName = "Team 1",
                    Total = 100
                }))
        };
        var interaction = new TestSlashCommandInteraction(operations);
        interaction.SetStringOption("around", "Ghost");
        var handler = CreateHandler(client);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Be(
            "🤷 Не удалось найти менеджера или команду \"Ghost\".");
    }

    [Test]
    public async Task HandleAsync_AroundAmbiguous_ReturnsCandidates()
    {
        // Arrange
        var operations = new List<string>();
        var client = new TestFantasyPremierLeagueClient(operations)
        {
            ClassicTask = Task.FromResult(CreateClassicResponse(
                new ClassicStanding
                {
                    Rank = 1,
                    Entry = 1,
                    EntryName = "Alex FC",
                    PlayerName = "Bob",
                    Total = 100
                },
                new ClassicStanding
                {
                    Rank = 2,
                    Entry = 2,
                    EntryName = "FC Alexander",
                    PlayerName = "Anna",
                    Total = 99
                }))
        };
        var interaction = new TestSlashCommandInteraction(operations);
        interaction.SetStringOption("around", "Alex");
        var handler = CreateHandler(client);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().StartWith(
            "🤔 Нашлось несколько вариантов для \"Alex\":");
        interaction.Messages[0].Should().Contain("Alex FC");
        interaction.Messages[0].Should().Contain("FC Alexander");
        interaction.Messages[0].Should().Contain("Уточните название команды или имя менеджера.");
    }

    [Test]
    public async Task HandleAsync_TopAndAround_ValidationErrorAndZeroApiCalls()
    {
        // Arrange
        var operations = new List<string>();
        var client = new TestFantasyPremierLeagueClient(operations);
        var interaction = new TestSlashCommandInteraction(operations);
        interaction.SetIntegerOption("top", 5);
        interaction.SetStringOption("around", "Bob");
        var handler = CreateHandler(client);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        client.ClassicCallCount.Should().Be(0);
        client.HeadToHeadCallCount.Should().Be(0);
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Be(
            "⚠️ Используйте либо top, либо around — эти режимы нельзя комбинировать.");
    }

    [Test]
    public async Task HandleAsync_AroundWithH2H_ValidationErrorAndZeroApiCalls()
    {
        // Arrange
        var operations = new List<string>();
        var client = new TestFantasyPremierLeagueClient(operations);
        var interaction = new TestSlashCommandInteraction(operations);
        interaction.SetStringOption("league", "h2h");
        interaction.SetStringOption("around", "Bob");
        var handler = CreateHandler(client);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        client.ClassicCallCount.Should().Be(0);
        client.HeadToHeadCallCount.Should().Be(0);
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Be(
            "⚠️ Режим around пока доступен только для классической лиги.");
    }

    [Test]
    public async Task HandleAsync_WhitespaceAround_ReturnsValidationErrorAndZeroApiCalls()
    {
        // Arrange
        var operations = new List<string>();
        var client = new TestFantasyPremierLeagueClient(operations);
        var interaction = new TestSlashCommandInteraction(operations);
        interaction.SetStringOption("around", "   ");
        var handler = CreateHandler(client);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        client.ClassicCallCount.Should().Be(0);
        client.HeadToHeadCallCount.Should().Be(0);
        operations.Should().NotContain("Defer");
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Be(
            "⚠️ Укажите имя менеджера или название команды для around.");
    }

    [Test]
    public async Task HandleAsync_ClassicOnlyLeagueFails_ReturnsUnavailableClassic()
    {
        // Arrange
        var logger = new RecordingLogger<StandingsCommandHandler>();
        var client = new TestFantasyPremierLeagueClient([])
        {
            ClassicTask = Task.FromException<ClassicStandingsResponse>(
                CreateApiException(FantasyPremierLeagueFailureKind.Transient))
        };
        var interaction = new TestSlashCommandInteraction([]);
        interaction.SetStringOption("league", "classic");
        var handler = CreateHandler(client, logger);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        client.HeadToHeadCallCount.Should().Be(0);
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Be(
            "🏆 Турнирная таблица классической лиги:\n⚠️ Эта лига сейчас недоступна.");
        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Properties["LeagueType"]!.Equals("Classic"));
    }

    [Test]
    public async Task HandleAsync_HeadToHeadOnlyLeagueFails_ReturnsUnavailableHeadToHead()
    {
        // Arrange
        var client = new TestFantasyPremierLeagueClient([])
        {
            HeadToHeadTask = Task.FromException<HeadToHeadStandingsResponse>(
                CreateApiException(FantasyPremierLeagueFailureKind.Transient))
        };
        var interaction = new TestSlashCommandInteraction([]);
        interaction.SetStringOption("league", "h2h");
        var handler = CreateHandler(client);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        client.ClassicCallCount.Should().Be(0);
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Be(
            "⚔️ Турнирная таблица лиги один на один:\n⚠️ Эта лига сейчас недоступна.");
    }

    [Test]
    public async Task HandleAsync_CombinedClassicFails_ReturnsHeadToHeadAndUnavailableWarning()
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
                    Total = 12,
                    MatchesPlayed = 7
                }))
        };
        var interaction = new TestSlashCommandInteraction([]);
        var handler = CreateHandler(client, logger);

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Contain(
            "🏆 Турнирная таблица классической лиги:\n⚠️ Эта лига сейчас недоступна.");
        interaction.Messages[0].Should().Contain("🥇 Available Team — 12 | 7 матчей");
        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Properties["LeagueType"]!.Equals("Classic"));
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
            "⚠️ Таблицы FPL сейчас недоступны. Попробуйте ещё раз позже.");
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
        interaction.Messages[0].Split("🤷 Данные о позициях не получены.")
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
                Entry = rank,
                EntryName = $"@everyone Classic team {rank:D2} with a deliberately long name",
                Total = 1_000 - rank,
                EventTotal = 10
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
                    Total = 15,
                    MatchesPlayed = 7
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
            new FplStandingsSelectionService(),
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

        public Task<EntryEventPicksResponse> GetEntryEventPicksAsync(
            int entryId,
            int eventId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<EventLiveResponse> GetEventLiveAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<PremierLeagueFixture>> GetFixturesAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestSlashCommandInteraction(List<string> operations)
        : IDiscordSlashCommandInteraction
    {
        private readonly Dictionary<string, string> _stringOptions = new();
        private readonly Dictionary<string, long> _integerOptions = new();

        public string Name => DiscordApplicationCommands.StandingsName;

        public string UserMention => "<@123>";

        public void SetStringOption(string name, string value) =>
            _stringOptions[name] = value;

        public void SetIntegerOption(string name, long value) =>
            _integerOptions[name] = value;

        public string? GetStringOption(string name) =>
            _stringOptions.TryGetValue(name, out var value) ? value : null;

        public long? GetIntegerOption(string name) =>
            _integerOptions.TryGetValue(name, out var value) ? value : null;

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
