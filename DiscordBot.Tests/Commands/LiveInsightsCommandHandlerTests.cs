using AwesomeAssertions;
using DiscordBot.Commands;
using DiscordBot.FantasyPremierLeague;
using DiscordBot.FantasyPremierLeague.Live;
using DiscordBot.Responses;
using NUnit.Framework;

namespace DiscordBot.Tests.Commands;

[TestFixture]
public sealed class LiveInsightsCommandHandlerTests
{
    [Test]
    public async Task HandleAsync_AvailableLiveData_DefersAndPublishesFormattedInsights()
    {
        // Arrange
        var options = new FantasyPremierLeagueOptions
        {
            ClassicLeagueId = 123,
            LiveDataMaxAge = TimeSpan.FromMinutes(20)
        };
        var service = new FplLiveInsightsService(
            new TestFantasyPremierLeagueClient(),
            options,
            new FplLiveInsightsCalculationService(options),
            new FixedTimeProvider(),
            new RecordingLogger<FplLiveInsightsService>());
        var interaction = new TestSlashCommandInteraction();
        var handler = new LiveInsightsCommandHandler(
            service,
            new FplLiveInsightsMessageComposer(options));

        // Act
        await handler.HandleAsync(interaction);

        // Assert
        interaction.Operations.Should().Equal("Defer", "Modify");
        interaction.Messages.Should().ContainSingle();
        interaction.Messages[0].Should().Contain("FPL в прямом эфире — тур 5");
        interaction.Messages[0].Should().Contain("Configured Team");
    }

    private sealed class TestFantasyPremierLeagueClient : IFantasyPremierLeagueClient
    {
        public Task<BootstrapStaticResponse> GetBootstrapStaticAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new BootstrapStaticResponse
            {
                Events =
                [
                    new PremierLeagueEvent
                    {
                        Id = 5,
                        IsCurrent = true,
                        DeadlineTimeEpoch = 1_787_333_400
                    }
                ],
                Elements =
                [
                    new PremierLeagueElement { Id = 1, WebName = "Captain" },
                    new PremierLeagueElement { Id = 2, WebName = "Vice" },
                    new PremierLeagueElement { Id = 3, WebName = "Bench" }
                ]
            });
        }

        public Task<ClassicStandingsResponse> GetClassicStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ClassicStandingsResponse
            {
                LastUpdatedData = new DateTimeOffset(
                    2026,
                    8,
                    21,
                    18,
                    45,
                    0,
                    TimeSpan.Zero),
                Standings = new ClassicStandings
                {
                    Results =
                    [
                        new ClassicStanding
                        {
                            Entry = 10,
                            EntryName = "Configured Team",
                            PlayerName = "Configured Manager",
                            Rank = 1
                        }
                    ]
                }
            });
        }

        public Task<HeadToHeadStandingsResponse> GetHeadToHeadStandingsAsync(
            int leagueId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<EntryEventPicksResponse> GetEntryEventPicksAsync(
            int entryId,
            int eventId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new EntryEventPicksResponse
            {
                Picks =
                [
                    new EntryEventPick
                    {
                        Element = 1,
                        Position = 1,
                        Multiplier = 2,
                        IsCaptain = true
                    },
                    new EntryEventPick
                    {
                        Element = 2,
                        Position = 2,
                        Multiplier = 1,
                        IsViceCaptain = true
                    },
                    new EntryEventPick
                    {
                        Element = 3,
                        Position = 12,
                        Multiplier = 0
                    }
                ]
            });
        }

        public Task<EventLiveResponse> GetEventLiveAsync(
            int eventId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new EventLiveResponse
            {
                Elements =
                [
                    new EventLiveElement
                    {
                        Id = 1,
                        Stats = new EventLiveElementStats
                        {
                            TotalPoints = 10,
                            Minutes = 90
                        }
                    },
                    new EventLiveElement
                    {
                        Id = 2,
                        Stats = new EventLiveElementStats
                        {
                            TotalPoints = 4,
                            Minutes = 90
                        }
                    },
                    new EventLiveElement
                    {
                        Id = 3,
                        Stats = new EventLiveElementStats
                        {
                            TotalPoints = 8,
                            Minutes = 90
                        }
                    }
                ]
            });
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(2026, 8, 21, 18, 50, 0, TimeSpan.Zero);
        }
    }

    private sealed class TestSlashCommandInteraction : IDiscordSlashCommandInteraction
    {
        public string Name => DiscordApplicationCommands.LiveInsightsName;

        public string UserMention => "<@123>";

        public List<string> Operations { get; } = [];

        public List<string> Messages { get; } = [];

        public Task RespondAsync(string content)
        {
            Operations.Add("Respond");
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task DeferAsync()
        {
            Operations.Add("Defer");
            return Task.CompletedTask;
        }

        public Task ModifyOriginalResponseAsync(string content)
        {
            Operations.Add("Modify");
            Messages.Add(content);
            return Task.CompletedTask;
        }

        public Task FollowupAsync(string content)
        {
            Operations.Add("Followup");
            Messages.Add(content);
            return Task.CompletedTask;
        }
    }
}
