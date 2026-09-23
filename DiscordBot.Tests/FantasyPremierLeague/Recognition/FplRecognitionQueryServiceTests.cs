using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague.Historical;
using DiscordBot.FantasyPremierLeague.Recognition;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague.Recognition;

[TestFixture]
public sealed class FplRecognitionQueryServiceTests
{
    private const int LeagueId = 123;
    private const string Season = "2026/27";

    [Test]
    public void GetManagerProfile_ExactTeamName_Matches()
    {
        var (service, _) = CreateService(
            latest: Snapshot(5, Manager(10, "Bobrov FC", "Aleksandrs Bobrovs")));

        var result = service.GetManagerProfile("Bobrov FC");

        result.Outcome.Should().Be(FplManagerProfileLookupOutcome.Available);
        result.Profile!.EntryId.Should().Be(10);
    }

    [Test]
    public void GetManagerProfile_ExactManagerName_Matches()
    {
        var (service, _) = CreateService(
            latest: Snapshot(5, Manager(10, "Bobrov FC", "Aleksandrs Bobrovs")));

        var result = service.GetManagerProfile("Aleksandrs Bobrovs");

        result.Outcome.Should().Be(FplManagerProfileLookupOutcome.Available);
        result.Profile!.EntryId.Should().Be(10);
    }

    [Test]
    public void GetManagerProfile_CaseInsensitive_Matches()
    {
        var (service, _) = CreateService(
            latest: Snapshot(5, Manager(10, "Bobrov FC", "Aleksandrs Bobrovs")));

        var result = service.GetManagerProfile("bobrov fc");

        result.Outcome.Should().Be(FplManagerProfileLookupOutcome.Available);
        result.Profile!.EntryId.Should().Be(10);
    }

    [Test]
    public void GetManagerProfile_UniquePartialTeamName_Matches()
    {
        var (service, _) = CreateService(
            latest: Snapshot(5,
                Manager(10, "Bobrov FC", "Aleksandrs Bobrovs"),
                Manager(20, "Fraud United", "Bob Smith")));

        var result = service.GetManagerProfile("rov");

        result.Outcome.Should().Be(FplManagerProfileLookupOutcome.Available);
        result.Profile!.EntryId.Should().Be(10);
    }

    [Test]
    public void GetManagerProfile_UniquePartialManagerName_Matches()
    {
        var (service, _) = CreateService(
            latest: Snapshot(5,
                Manager(10, "Bobrov FC", "Aleksandrs Bobrovs"),
                Manager(20, "Fraud United", "Bob Smith")));

        var result = service.GetManagerProfile("leks");

        result.Outcome.Should().Be(FplManagerProfileLookupOutcome.Available);
        result.Profile!.EntryId.Should().Be(10);
    }

    [Test]
    public void GetManagerProfile_NoMatch_ReturnsNotFound()
    {
        var (service, _) = CreateService(
            latest: Snapshot(5, Manager(10, "Bobrov FC", "Aleksandrs Bobrovs")));

        var result = service.GetManagerProfile("zzz");

        result.Outcome.Should().Be(FplManagerProfileLookupOutcome.NotFound);
    }

    [Test]
    public void GetManagerProfile_AmbiguousPartialMatch_ReturnsCandidates()
    {
        var (service, _) = CreateService(
            latest: Snapshot(5,
                Manager(10, "Bobrov FC", "Aleksandrs Bobrovs"),
                Manager(20, "Bob United", "Bob Smith")));

        var result = service.GetManagerProfile("bob");

        result.Outcome.Should().Be(FplManagerProfileLookupOutcome.Ambiguous);
        result.Candidates.Should().HaveCount(2);
    }

    [Test]
    public void GetManagerProfile_HistoricalIdentity_UsesLatestName()
    {
        var latest = Snapshot(5, Manager(10, "Renamed FC", "Aleksandrs Bobrovs"));
        var earlier = Snapshot(1, Manager(10, "Original FC", "Aleksandrs Bobrovs"));
        var awards = new[]
        {
            Award(1, 10, "Original FC", FplAchievementKeys.BenchWarmer, true)
        };
        var (service, _) = CreateService(
            latest: latest,
            extraSnapshots: [earlier],
            awards: awards);

        var result = service.GetManagerProfile("Renamed FC");

        result.Outcome.Should().Be(FplManagerProfileLookupOutcome.Available);
        result.Profile!.EntryName.Should().Be("Renamed FC");
        result.Profile!.AchievementCounts.Should().ContainSingle()
            .Which.Count.Should().Be(1);
    }

    [Test]
    public void GetManagerProfile_AchievementAggregation_CountsPerKey()
    {
        var awards = new List<FplAchievementAward>();
        for (var i = 0; i < 4; i++)
        {
            awards.Add(Award(1 + i, 10, "Bobrov FC", FplAchievementKeys.BenchWarmer, true));
        }

        for (var i = 0; i < 2; i++)
        {
            awards.Add(Award(1 + i, 10, "Bobrov FC", FplAchievementKeys.CaptainDisaster, true));
        }

        awards.Add(Award(1, 10, "Bobrov FC", FplAchievementKeys.FirstBlood, false));

        var (service, _) = CreateService(
            latest: Snapshot(5, Manager(10, "Bobrov FC", "Aleksandrs Bobrovs")),
            awards: awards);

        var result = service.GetManagerProfile("Bobrov FC");

        result.Outcome.Should().Be(FplManagerProfileLookupOutcome.Available);
        var counts = result.Profile!.AchievementCounts;
        counts.Single(c => c.AchievementKey == FplAchievementKeys.BenchWarmer).Count
            .Should().Be(4);
        counts.Single(c => c.AchievementKey == FplAchievementKeys.CaptainDisaster).Count
            .Should().Be(2);
        counts.Single(c => c.AchievementKey == FplAchievementKeys.FirstBlood).Count
            .Should().Be(1);
        result.Profile!.TotalAchievements.Should().Be(7);
    }

    [Test]
    public void GetManagerProfile_RetiredDifferentialMerchant_IsIgnored()
    {
        var awards = new List<FplAchievementAward>
        {
            Award(1, 10, "Bobrov FC", FplAchievementKeys.BenchWarmer, true),
            Award(1, 10, "Bobrov FC", RetiredFplAchievementKeys.DifferentialMerchant, true),
            Award(2, 10, "Bobrov FC", RetiredFplAchievementKeys.DifferentialMerchant, true),
            Award(3, 10, "Bobrov FC", RetiredFplAchievementKeys.DifferentialMerchant, true)
        };
        var (service, _) = CreateService(
            latest: Snapshot(5, Manager(10, "Bobrov FC", "Aleksandrs Bobrovs")),
            awards: awards);

        var result = service.GetManagerProfile("Bobrov FC");

        result.Outcome.Should().Be(FplManagerProfileLookupOutcome.Available);
        result.Profile!.AchievementCounts.Should().ContainSingle()
            .Which.AchievementKey.Should().Be(FplAchievementKeys.BenchWarmer);
        result.Profile!.TotalAchievements.Should().Be(1);
    }

    [Test]
    public void GetSeasonSummary_RetiredDifferentialMerchant_IsIgnored()
    {
        var awards = new[]
        {
            Award(1, 10, "Bobrov FC", FplAchievementKeys.BenchWarmer, true),
            Award(1, 10, "Bobrov FC", RetiredFplAchievementKeys.DifferentialMerchant, true),
            Award(2, 10, "Bobrov FC", RetiredFplAchievementKeys.DifferentialMerchant, true),
            Award(1, 20, "Rival FC", RetiredFplAchievementKeys.DifferentialMerchant, true),
            Award(2, 20, "Rival FC", RetiredFplAchievementKeys.DifferentialMerchant, true)
        };
        var (service, _) = CreateService(
            latest: Snapshot(5,
                Manager(10, "Bobrov FC", "A"),
                Manager(20, "Rival FC", "B")),
            awards: awards);

        var summary = service.GetSeasonSummary();

        summary.Should().NotBeNull();
        summary!.TotalAwardRanking.Should().ContainSingle()
            .Which.EntryId.Should().Be(10);
        summary.TotalAwardRanking.Single().TotalAwards.Should().Be(1);
        summary.PerAchievementLeaders
            .Should().NotContain(c => RetiredFplAchievementKeys.IsRetired(c.AchievementKey));
        summary.PerAchievementLeaders.Single(c => c.AchievementKey == FplAchievementKeys.BenchWarmer)
            .Leaders.Single().EntryId.Should().Be(10);
    }

    [Test]
    public void GetManagerProfile_GameweekWins_CountsSoleAndTiedWinners()
    {
        var snapshots = new[]
        {
            Snapshot(1,
                Manager(10, "Bobrov FC", "A", eventScore: 80),
                Manager(20, "Rival FC", "B", eventScore: 70)),
            Snapshot(2,
                Manager(10, "Bobrov FC", "A", eventScore: 60),
                Manager(20, "Rival FC", "B", eventScore: 60)),
            Snapshot(3,
                Manager(10, "Bobrov FC", "A", eventScore: 50),
                Manager(20, "Rival FC", "B", eventScore: 90))
        };
        var (service, _) = CreateService(
            latest: snapshots[^1],
            extraSnapshots: snapshots[..^1],
            awards: []);

        var sole = service.GetManagerProfile("Bobrov FC");
        sole.Profile!.GameweekWins.Should().Be(2);

        var rival = service.GetManagerProfile("Rival FC");
        rival.Profile!.GameweekWins.Should().Be(2);
    }

    [Test]
    public void GetManagerProfile_GameweekWins_RespectsTrackingStart()
    {
        var snapshots = new[]
        {
            Snapshot(1, Manager(10, "Bobrov FC", "A", eventScore: 99)),
            Snapshot(2, Manager(10, "Bobrov FC", "A", eventScore: 99)),
            Snapshot(3, Manager(10, "Bobrov FC", "A", eventScore: 99)),
            Snapshot(4, Manager(10, "Bobrov FC", "A", eventScore: 99)),
            Snapshot(5, Manager(10, "Bobrov FC", "A", eventScore: 99))
        };
        var (service, _) = CreateService(
            latest: snapshots[^1],
            extraSnapshots: snapshots[..^1],
            trackingStart: 3,
            awards: []);

        var result = service.GetManagerProfile("Bobrov FC");

        result.Profile!.GameweekWins.Should().Be(3);
    }

    [Test]
    public void GetManagerProfile_PersonalRecords_AreDeterministicOnTie()
    {
        var snapshots = new[]
        {
            Snapshot(2, Manager(10, "Bobrov FC", "A", eventScore: 90, benchPoints: 10)),
            Snapshot(4, Manager(10, "Bobrov FC", "A", eventScore: 90, benchPoints: 24)),
            Snapshot(3, Manager(10, "Bobrov FC", "A", eventScore: 80, benchPoints: 24)),
            Snapshot(5, Manager(10, "Bobrov FC", "A", eventScore: 70, benchPoints: 5))
        };
        var (service, _) = CreateService(
            latest: snapshots[^1],
            extraSnapshots: snapshots[..^1],
            awards: []);

        var result = service.GetManagerProfile("Bobrov FC");

        result.Profile!.BestGameweekScore.Should().Be(90);
        result.Profile!.BestGameweekEventId.Should().Be(2);
        result.Profile!.HighestBenchPoints.Should().Be(24);
        result.Profile!.HighestBenchPointsEventId.Should().Be(3);
    }

    [Test]
    public void GetManagerProfile_PreviousSeason_NotInCurrentProfile()
    {
        var current = Snapshot(5, Manager(10, "Bobrov FC", "A", eventScore: 50));
        var previous = Snapshot(5, [Manager(10, "Bobrov FC", "A", eventScore: 99)], "2025/26");
        var awards = new[]
        {
            Award(5, 10, "Bobrov FC", FplAchievementKeys.BenchWarmer, true, season: "2025/26")
        };
        var (service, _) = CreateService(
            latest: current,
            extraSnapshots: [previous],
            awards: awards);

        var result = service.GetManagerProfile("Bobrov FC");

        result.Profile!.AchievementCounts.Should().BeEmpty();
        result.Profile!.BestGameweekScore.Should().Be(50);
    }

    [Test]
    public void GetSeasonSummary_TotalRanking_HandlesTies()
    {
        var awards = new[]
        {
            Award(1, 10, "Bobrov FC", FplAchievementKeys.BenchWarmer, true),
            Award(2, 10, "Bobrov FC", FplAchievementKeys.BenchWarmer, true),
            Award(1, 20, "Rival FC", FplAchievementKeys.BenchWarmer, true),
            Award(2, 20, "Rival FC", FplAchievementKeys.BenchWarmer, true)
        };
        var (service, _) = CreateService(
            latest: Snapshot(5,
                Manager(10, "Bobrov FC", "A", rank: 4),
                Manager(20, "Rival FC", "B", rank: 2)),
            awards: awards);

        var summary = service.GetSeasonSummary();

        summary.Should().NotBeNull();
        var tied = summary!.TotalAwardRanking
            .Where(entry => entry.TotalAwards == 2)
            .ToArray();
        tied.Should().HaveCount(2);
    }

    [Test]
    public void GetSeasonSummary_PerAchievementCategories_ArePopulated()
    {
        var awards = new[]
        {
            Award(1, 10, "Bobrov FC", FplAchievementKeys.BenchWarmer, true),
            Award(1, 20, "Rival FC", FplAchievementKeys.CaptainDisaster, true)
        };
        var (service, _) = CreateService(
            latest: Snapshot(5,
                Manager(10, "Bobrov FC", "A"),
                Manager(20, "Rival FC", "B")),
            awards: awards);

        var summary = service.GetSeasonSummary();

        summary!.PerAchievementLeaders.Should().HaveCount(2);
        summary.PerAchievementLeaders
            .Single(c => c.AchievementKey == FplAchievementKeys.BenchWarmer)
            .Leaders.Single().EntryId.Should().Be(10);
    }

    [Test]
    public void GetSeasonSummary_NoData_ReturnsNull()
    {
        var store = new FakeRecognitionStore { LatestSeason = null };
        var statistics = new FakeStatisticsStore();
        var service = new FplRecognitionQueryService(
            Options(),
            store,
            statistics);

        service.GetSeasonSummary().Should().BeNull();
    }

    [Test]
    public void GetSeasonSummary_PerAchievementLeaderWhoLeftLeague_IsIgnored()
    {
        var awards = new[]
        {
            Award(1, 99, "Departed FC", FplAchievementKeys.BenchWarmer, true),
            Award(2, 99, "Departed FC", FplAchievementKeys.BenchWarmer, true),
            Award(3, 99, "Departed FC", FplAchievementKeys.BenchWarmer, true),
            Award(1, 10, "Bobrov FC", FplAchievementKeys.BenchWarmer, true),
            Award(2, 10, "Bobrov FC", FplAchievementKeys.BenchWarmer, true)
        };
        var (service, _) = CreateService(
            latest: Snapshot(5, Manager(10, "Bobrov FC", "A")),
            awards: awards);

        var summary = service.GetSeasonSummary();

        var category = summary!.PerAchievementLeaders
            .Single(x => x.AchievementKey == FplAchievementKeys.BenchWarmer);
        category.Count.Should().Be(2);
        category.Leaders.Single().EntryId.Should().Be(10);
    }

    [Test]
    public void GetSeasonSummary_GameweekWinLeaderWhoLeftLeague_IsIgnored()
    {
        var snapshots = new[]
        {
            Snapshot(1, Manager(99, "Departed FC", "Z", eventScore: 100)),
            Snapshot(2, Manager(10, "Bobrov FC", "A", eventScore: 90))
        };
        var (service, _) = CreateService(
            latest: snapshots[^1],
            extraSnapshots: snapshots[..^1],
            awards: []);

        var summary = service.GetSeasonSummary();

        summary!.GameweekWinTopCount.Should().Be(1);
        summary.GameweekWinLeaders.Single().EntryId.Should().Be(10);
    }

    private static (
        FplRecognitionQueryService Service,
        FakeRecognitionStore Store)
        CreateService(
            FplGameweekSnapshot latest,
            FplGameweekSnapshot[]? extraSnapshots = null,
            IReadOnlyList<FplAchievementAward>? awards = null,
            int trackingStart = 1)
    {
        var snapshots = new List<FplGameweekSnapshot> { latest };
        if (extraSnapshots is not null)
        {
            snapshots.AddRange(extraSnapshots);
        }

        var store = new FakeRecognitionStore
        {
            LatestSeason = Season,
            FirstCompletedEventId = trackingStart,
            Awards = awards ?? []
        };
        var statistics = new FakeStatisticsStore { Snapshots = snapshots };
        var service = new FplRecognitionQueryService(Options(), store, statistics);
        return (service, store);
    }

    private static FantasyPremierLeagueOptions Options() => new()
    {
        ClassicLeagueId = LeagueId
    };

    private static FplGameweekSnapshot Snapshot(
        int eventId,
        params FplManagerGameweekStatistics[] managers)
        => Snapshot(eventId, managers, Season);

    private static FplGameweekSnapshot Snapshot(
        int eventId,
        FplManagerGameweekStatistics[] managers,
        string season)
        => new(
            season,
            eventId,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            managers);

    private static FplManagerGameweekStatistics Manager(
        int entryId,
        string entryName,
        string managerName,
        int eventScore = 50,
        int benchPoints = 0,
        int rank = 1)
        => new(
            entryId,
            entryName,
            managerName,
            eventScore,
            eventScore * 10,
            rank,
            rank,
            0,
            benchPoints,
            []);

    private static FplAchievementAward Award(
        int eventId,
        int entryId,
        string entryName,
        string achievementKey,
        bool isRepeatable,
        string season = Season)
        => new(
            LeagueId,
            season,
            eventId,
            entryId,
            entryName,
            achievementKey,
            achievementKey,
            "Test achievement",
            isRepeatable,
            "v1",
            DateTimeOffset.UnixEpoch);

    private sealed class FakeRecognitionStore : IFplRecognitionStore
    {
        public string? LatestSeason { get; init; }

        public int? FirstCompletedEventId { get; init; }

        public IReadOnlyList<FplAchievementAward> Awards { get; init; } = [];

        public string? GetLatestSeason(int leagueId) => LatestSeason;

        public int? GetFirstCompletedEventId(int leagueId, string season)
            => FirstCompletedEventId;

        public IReadOnlyList<FplAchievementAward> GetAchievementAwards(
            int leagueId,
            string season,
            int? eventId = null)
            => Awards.Where(award => award.Season == season).ToArray();

        public FplRecognitionResult? GetCompletedResult(
            int leagueId,
            string season,
            int eventId) => throw new NotSupportedException();

        public void Save(FplRecognitionRun run, FplRecognitionResult result)
            => throw new NotSupportedException();
    }

    private sealed class FakeStatisticsStore : IFplStatisticsStore
    {
        public IReadOnlyList<FplGameweekSnapshot> Snapshots { get; init; } = [];

        public bool IsSnapshotStored(string season, int eventId)
            => throw new NotSupportedException();

        public void SaveSnapshot(FplGameweekSnapshot snapshot)
            => throw new NotSupportedException();

        public FplGameweekSnapshot? GetSnapshot(
            string season,
            int eventId) => throw new NotSupportedException();

        public IReadOnlyList<FplGameweekSnapshot> GetSnapshots(
            string season,
            int? eventId = null,
            int? managerEntryId = null)
            => Snapshots.Where(snapshot => snapshot.Season == season).ToArray();
    }
}
