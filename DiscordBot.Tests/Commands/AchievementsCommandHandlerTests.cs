using AwesomeAssertions;
using DiscordBot.Commands;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recognition;
using NUnit.Framework;

namespace DiscordBot.Tests.Commands;

[TestFixture]
public sealed class AchievementsCommandHandlerTests
{
    [Test]
    public async Task HandleAsync_DefersBeforeWork()
    {
        var query = new FakeQueryService(CreateSummary());
        var handler = new AchievementsCommandHandler(
            query,
            new FplRecognitionMessageCompositionService());
        var interaction = new TestInteraction();

        await handler.HandleAsync(interaction);

        interaction.Operations.Should().StartWith("Defer");
    }

    [Test]
    public async Task HandleAsync_NoData_RespondsFriendly()
    {
        var query = new FakeQueryService(null);
        var handler = new AchievementsCommandHandler(
            query,
            new FplRecognitionMessageCompositionService());
        var interaction = new TestInteraction();

        await handler.HandleAsync(interaction);

        interaction.Messages.Should().ContainSingle()
            .Which.Should().Contain("Данные о достижениях пока недоступны");
    }

    [Test]
    public async Task HandleAsync_Success_RendersLeaderboard()
    {
        var query = new FakeQueryService(CreateSummary());
        var handler = new AchievementsCommandHandler(
            query,
            new FplRecognitionMessageCompositionService());
        var interaction = new TestInteraction();

        await handler.HandleAsync(interaction);

        interaction.Messages.Should().ContainSingle()
            .Which.Should().Contain("🏆 Достижения — сезон 2026/27");
    }

    [Test]
    public async Task HandleAsync_LongSummary_SplitsIntoChunks()
    {
        var query = new FakeQueryService(CreateLongSummary());
        var handler = new AchievementsCommandHandler(
            query,
            new FplRecognitionMessageCompositionService());
        var interaction = new TestInteraction();

        await handler.HandleAsync(interaction);

        interaction.Operations.Should().Contain("Followup");
        interaction.Messages.Count.Should().BeGreaterThan(1);
    }

    private static FplAchievementSeasonSummary CreateSummary() => new(
        "2026/27",
        1,
        [new FplAchievementTotalRankingEntry(10, "Bobrov FC", "A", 4, 10)],
        [new FplAchievementCategoryRanking(
            FplAchievementKeys.BenchWarmer,
            4,
            [new FplManagerReference(10, "Bobrov FC", "A")])],
        4,
        [new FplManagerReference(10, "Bobrov FC", "A")]);

    private static FplAchievementSeasonSummary CreateLongSummary()
    {
        var winners = new List<FplManagerReference>();
        for (var i = 0; i < 200; i++)
        {
            winners.Add(new FplManagerReference(
                i + 1,
                $"Team {i + 1}",
                "Manager"));
        }

        return new FplAchievementSeasonSummary(
            "2026/27",
            1,
            [],
            [],
            1,
            winners);
    }

    private sealed class FakeQueryService : FplRecognitionQueryService
    {
        private readonly FplAchievementSeasonSummary? _summary;

        public FakeQueryService(FplAchievementSeasonSummary? summary)
            : base(
                new FantasyPremierLeagueOptions { ClassicLeagueId = 1 },
                new FakeRecognitionStore(),
                new FakeStatisticsStore())
        {
            _summary = summary;
        }

        public override FplAchievementSeasonSummary? GetSeasonSummary() => _summary;
    }

    private sealed class FakeRecognitionStore : IFplRecognitionStore
    {
        public string? GetLatestSeason(int leagueId) => null;

        public int? GetFirstCompletedEventId(int leagueId, string season) => null;

        public IReadOnlyList<FplAchievementAward> GetAchievementAwards(
            int leagueId,
            string season,
            int? eventId = null) => [];

        public FplRecognitionResult? GetCompletedResult(
            int leagueId,
            string season,
            int eventId) => null;

        public void Save(FplRecognitionRun run, FplRecognitionResult result)
        {
        }
    }

    private sealed class FakeStatisticsStore : IFplStatisticsStore
    {
        public bool IsSnapshotStored(string season, int eventId) => false;

        public void SaveSnapshot(FplGameweekSnapshot snapshot)
        {
        }

        public FplGameweekSnapshot? GetSnapshot(string season, int eventId) => null;

        public IReadOnlyList<FplGameweekSnapshot> GetSnapshots(
            string season,
            int? eventId = null,
            int? managerEntryId = null) => [];
    }

    private sealed class TestInteraction : IDiscordSlashCommandInteraction
    {
        public string Name => DiscordApplicationCommands.AchievementsName;

        public string UserMention => "<@1>";

        public List<string> Messages { get; } = [];

        public List<string> Operations { get; } = [];

        public string? GetStringOption(string name) => null;

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
