using AwesomeAssertions;
using DiscordBot.Commands;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recognition;
using NUnit.Framework;

namespace DiscordBot.Tests.Commands;

[TestFixture]
public sealed class ProfileCommandHandlerTests
{
    [Test]
    public async Task HandleAsync_DefersBeforeWork()
    {
        var query = new FakeQueryService(
            FplManagerProfileLookupResult.ForNoData());
        var handler = new ProfileCommandHandler(query, new FplRecognitionMessageCompositionService());
        var interaction = new TestInteraction();

        await handler.HandleAsync(interaction);

        interaction.Operations.Should().StartWith("Defer");
    }

    [Test]
    public async Task HandleAsync_ReadsManagerOption()
    {
        var query = new FakeQueryService(
            FplManagerProfileLookupResult.ForNotFound());
        var handler = new ProfileCommandHandler(query, new FplRecognitionMessageCompositionService());
        var interaction = new TestInteraction(new Dictionary<string, string>(StringComparer.Ordinal) { ["manager"] = "Bobrov" });

        await handler.HandleAsync(interaction);

        query.LastProfileQuery.Should().Be("Bobrov");
    }

    [Test]
    public async Task HandleAsync_AvailableProfile_RespondsWithProfile()
    {
        var profile = CreateProfile();
        var query = new FakeQueryService(
            FplManagerProfileLookupResult.ForAvailable(profile));
        var handler = new ProfileCommandHandler(query, new FplRecognitionMessageCompositionService());
        var interaction = new TestInteraction(new Dictionary<string, string>(StringComparer.Ordinal) { ["manager"] = "Bobrov FC" });

        await handler.HandleAsync(interaction);

        interaction.Messages.Should().ContainSingle()
            .Which.Should().Contain("🏅 Bobrov FC");
    }

    [Test]
    public async Task HandleAsync_NoData_RespondsWithNoData()
    {
        var query = new FakeQueryService(FplManagerProfileLookupResult.ForNoData());
        var handler = new ProfileCommandHandler(query, new FplRecognitionMessageCompositionService());
        var interaction = new TestInteraction(new Dictionary<string, string>(StringComparer.Ordinal) { ["manager"] = "Bobrov FC" });

        await handler.HandleAsync(interaction);

        interaction.Messages.Should().ContainSingle()
            .Which.Should().Contain("Данные о достижениях пока недоступны");
    }

    [Test]
    public async Task HandleAsync_NotFound_RespondsFriendly()
    {
        var query = new FakeQueryService(FplManagerProfileLookupResult.ForNotFound());
        var handler = new ProfileCommandHandler(query, new FplRecognitionMessageCompositionService());
        var interaction = new TestInteraction(new Dictionary<string, string>(StringComparer.Ordinal) { ["manager"] = "Ghost" });

        await handler.HandleAsync(interaction);

        interaction.Messages.Should().ContainSingle()
            .Which.Should().Contain("Не удалось найти менеджера");
    }

    [Test]
    public async Task HandleAsync_Ambiguous_RespondsWithCandidates()
    {
        var query = new FakeQueryService(
            FplManagerProfileLookupResult.ForAmbiguous(
            [
                new FplManagerReference(10, "Bobrov FC", "Aleksandrs Bobrovs"),
                new FplManagerReference(20, "Bob United", "Bob Smith")
            ]));
        var handler = new ProfileCommandHandler(query, new FplRecognitionMessageCompositionService());
        var interaction = new TestInteraction(new Dictionary<string, string>(StringComparer.Ordinal) { ["manager"] = "bob" });

        await handler.HandleAsync(interaction);

        interaction.Messages.Should().ContainSingle()
            .Which.Should().Contain("Нашлось несколько подходящих менеджеров");
    }

    [Test]
    public async Task HandleAsync_BlankOption_RespondsFriendly()
    {
        var query = new FakeQueryService(FplManagerProfileLookupResult.ForNotFound());
        var handler = new ProfileCommandHandler(query, new FplRecognitionMessageCompositionService());
        var interaction = new TestInteraction();

        await handler.HandleAsync(interaction);

        interaction.Messages.Should().ContainSingle()
            .Which.Should().Contain("Укажите название команды или имя менеджера");
    }

    [Test]
    public async Task HandleAsync_LongProfile_SplitsIntoChunks()
    {
        var profile = CreateLongProfile();
        var query = new FakeQueryService(
            FplManagerProfileLookupResult.ForAvailable(profile));
        var handler = new ProfileCommandHandler(query, new FplRecognitionMessageCompositionService());
        var interaction = new TestInteraction(new Dictionary<string, string>(StringComparer.Ordinal) { ["manager"] = "Bobrov FC" });

        await handler.HandleAsync(interaction);

        interaction.Operations.Should().Contain("Followup");
        interaction.Messages.Count.Should().BeGreaterThan(1);
    }

    private static FplManagerProfile CreateProfile() => new(
        "2026/27",
        1,
        10,
        "Bobrov FC",
        "Aleksandrs Bobrovs",
        4,
        612,
        2,
        91,
        5,
        24,
        3,
        [new FplAchievementCount(FplAchievementKeys.BenchWarmer, "bench", 1)]);

    private static FplManagerProfile CreateLongProfile()
    {
        var counts = new List<FplAchievementCount>();
        for (var i = 0; i < 90; i++)
        {
            counts.Add(new FplAchievementCount(
                FplAchievementKeys.BenchWarmer,
                "bench",
                1));
        }

        return new FplManagerProfile(
            "2026/27",
            1,
            10,
            "Bobrov FC",
            "Aleksandrs Bobrovs",
            4,
            612,
            2,
            91,
            5,
            24,
            3,
            counts);
    }

    private sealed class FakeQueryService : FplRecognitionQueryService
    {
        private readonly FplManagerProfileLookupResult _profileResult;

        public string? LastProfileQuery { get; private set; }

        public FakeQueryService(FplManagerProfileLookupResult profileResult)
            : base(
                new FantasyPremierLeagueOptions { ClassicLeagueId = 1 },
                new FakeRecognitionStore(),
                new FakeStatisticsStore())
        {
            _profileResult = profileResult;
        }

        public override FplManagerProfileLookupResult GetManagerProfile(string managerQuery)
        {
            LastProfileQuery = managerQuery;
            return _profileResult;
        }
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
        private readonly Dictionary<string, string> _options;

        public TestInteraction(Dictionary<string, string>? options = null)
        {
            _options = options ?? [];
        }

        public string Name => DiscordApplicationCommands.ProfileName;

        public string UserMention => "<@1>";

        public List<string> Messages { get; } = [];

        public List<string> Operations { get; } = [];

        public string? GetStringOption(string name)
            => _options.TryGetValue(name, out var value) ? value : null;

        public long? GetIntegerOption(string name) => null;

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
