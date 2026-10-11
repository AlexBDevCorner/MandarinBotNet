using System.Net;
using System.Text;
using AwesomeAssertions;
using DiscordBot.UclFantasy;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DiscordBot.Tests.UclFantasy;

[TestFixture]
public sealed class UclFantasyStandingsClientTests
{
    private const int LeagueId = 25247707;

    [Test]
    public async Task GetLeagueStandingsAsync_TwoPages_ReturnsCompleteTable()
    {
        // Arrange: page 1 has 2 of 3 members, page 2 has the last one.
        var handler = new StubHttpMessageHandler((_, request) =>
        {
            var query = request.RequestUri!.Query;
            var page = query.Contains("vPageNo=2", StringComparison.Ordinal) ? 2 : 1;
            return Task.FromResult(page == 1
                ? CreateJsonResponse(PageOneJson())
                : CreateJsonResponse(PageTwoJson()));
        });
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();
        var query = CreateQuery(pageSize: 2, maxPages: 10);

        // Act
        var result = await client.GetLeagueStandingsAsync(query, CancellationToken.None);

        // Assert
        result.LeagueId.Should().Be(LeagueId);
        result.LeagueName.Should().Be("Great Messi League");
        result.PhaseId.Should().Be(1);
        result.MatchdayId.Should().Be(2);
        result.Members.Should().HaveCount(3);
        result.Members.Select(member => member.TeamName).Should()
            .Equal("Alpha FC", "Beta United", "Gamma City");
        handler.RequestUris.Should().HaveCount(2);
        handler.RequestUris.Should().OnlyContain(uri =>
            !uri.Query.Contains("buster", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task GetLeagueStandingsAsync_RequestUri_ContainsObservedParametersWithoutBuster()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(SinglePageJson())));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();

        // Act
        await client.GetLeagueStandingsAsync(CreateQuery(), CancellationToken.None);

        // Assert
        var uri = handler.RequestUris.Should().ContainSingle().Subject;
        uri.AbsolutePath.Should().Contain($"/Leagues/{LeagueId}/leagueleaderboard");
        uri.Query.Should().Contain("optType=1");
        uri.Query.Should().Contain("phaseId=1");
        uri.Query.Should().Contain("matchdayId=2");
        uri.Query.Should().Contain("vPageChunk=25");
        uri.Query.Should().Contain("vPageNo=1");
        uri.Query.Should().Contain("vPageOneChunk=25");
        uri.Query.Should().Contain($"leagueID={LeagueId}");
        uri.Query.Should().NotContain("buster");
    }

    [Test]
    public async Task GetLeagueStandingsAsync_TiedRanks_PreservesTiesAndDeterministicOrder()
    {
        // Arrange: two members share rank 1; input order is reversed on retry.
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(TiedRanksJson())));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();

        // Act
        var first = await client.GetLeagueStandingsAsync(CreateQuery(), CancellationToken.None);
        var second = await client.GetLeagueStandingsAsync(CreateQuery(), CancellationToken.None);

        // Assert
        first.Members.Where(member => member.Rank == 1).Should().HaveCount(2);
        first.Members.Select(member => member.StableId).Should()
            .Equal(second.Members.Select(member => member.StableId));
    }

    [Test]
    public async Task GetLeagueStandingsAsync_AlternativeFieldNames_NormalizesMembers()
    {
        // Arrange: uses entries/entryName/points aliases instead of
        // leaderboard/teamName/total.
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(AlternativeNamesJson())));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();

        // Act
        var result = await client.GetLeagueStandingsAsync(CreateQuery(), CancellationToken.None);

        // Assert
        var member = result.Members.Should().ContainSingle().Subject;
        member.StableId.Should().Be(9001);
        member.TeamName.Should().Be("Alias FC");
        member.ManagerName.Should().Be("Alias Manager");
        member.Rank.Should().Be(1);
        member.TotalPoints.Should().Be(150);
        member.MatchdayPoints.Should().Be(45);
    }

    [Test]
    public async Task GetLeagueStandingsAsync_EmptyMembers_ThrowsInvalidPayload()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(EmptyMembersJson())));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();

        // Act
        Func<Task> act = () => client.GetLeagueStandingsAsync(CreateQuery(), CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<UclFantasyApiException>();
        exception.Which.FailureKind.Should().Be(UclFantasyFailureKind.InvalidPayload);
    }

    [Test]
    public async Task GetLeagueStandingsAsync_MemberWithoutIdentity_ThrowsInvalidPayload()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(MissingIdentityJson())));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();

        // Act
        Func<Task> act = () => client.GetLeagueStandingsAsync(CreateQuery(), CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<UclFantasyApiException>();
        exception.Which.FailureKind.Should().Be(UclFantasyFailureKind.InvalidPayload);
    }

    [Test]
    public async Task GetLeagueStandingsAsync_InconsistentTotalAcrossPages_ThrowsInvalidPayload()
    {
        // Arrange: page 1 says total 3, page 2 says total 4.
        var handler = new StubHttpMessageHandler((_, request) =>
        {
            var page = request.RequestUri!.Query.Contains("vPageNo=2", StringComparison.Ordinal) ? 2 : 1;
            return Task.FromResult(page == 1
                ? CreateJsonResponse(PageOneJson())
                : CreateJsonResponse(PageTwoDifferentTotalJson()));
        });
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();

        // Act
        Func<Task> act = () => client.GetLeagueStandingsAsync(
            CreateQuery(pageSize: 2, maxPages: 10),
            CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<UclFantasyApiException>();
        exception.Which.FailureKind.Should().Be(UclFantasyFailureKind.InvalidPayload);
    }

    [Test]
    public async Task GetLeagueStandingsAsync_FullPagesAtMaxBound_ThrowsInvalidPayload()
    {
        // Arrange: every page is full, so MaxPages cannot prove completeness.
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(FullPageNoTotalJson())));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();

        // Act
        Func<Task> act = () => client.GetLeagueStandingsAsync(
            CreateQuery(pageSize: 2, maxPages: 2),
            CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<UclFantasyApiException>();
        exception.Which.FailureKind.Should().Be(UclFantasyFailureKind.InvalidPayload);
    }

    [Test]
    public async Task GetLeagueStandingsAsync_OverlappingIdsWithoutTotal_ThrowsInvalidPayload()
    {
        // Arrange: no authoritative total; page 1 is full (3 members) and
        // page 2 is short (2 members) with one repeated stable ID. The
        // overlap must be rejected rather than silently dropped and accepted
        // as a complete table via the short final page.
        var handler = new StubHttpMessageHandler((_, request) =>
        {
            var page = request.RequestUri!.Query.Contains("vPageNo=2", StringComparison.Ordinal) ? 2 : 1;
            return Task.FromResult(page == 1
                ? CreateJsonResponse(OverlapPageOneNoTotalJson())
                : CreateJsonResponse(OverlapPageTwoNoTotalJson()));
        });
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();

        // Act
        Func<Task> act = () => client.GetLeagueStandingsAsync(
            CreateQuery(pageSize: 3, maxPages: 10),
            CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<UclFantasyApiException>();
        exception.Which.FailureKind.Should().Be(UclFantasyFailureKind.InvalidPayload);
    }

    [Test]
    public async Task GetLeagueStandingsAsync_OverlappingIdsWithTotal_ThrowsInvalidPayload()
    {
        // Arrange: even when the deduplicated count still matches the
        // advertised total, a repeated stable ID signals overlapping or
        // shifted pages and must not be silently accepted as complete.
        var handler = new StubHttpMessageHandler((_, request) =>
        {
            var page = request.RequestUri!.Query.Contains("vPageNo=2", StringComparison.Ordinal) ? 2 : 1;
            return Task.FromResult(page == 1
                ? CreateJsonResponse(OverlapPageOneWithTotalJson())
                : CreateJsonResponse(OverlapPageTwoWithTotalJson()));
        });
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();

        // Act
        Func<Task> act = () => client.GetLeagueStandingsAsync(
            CreateQuery(pageSize: 2, maxPages: 10),
            CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<UclFantasyApiException>();
        exception.Which.FailureKind.Should().Be(UclFantasyFailureKind.InvalidPayload);
    }

    [Test]
    public async Task GetLeagueStandingsAsync_NotFound_ClassifiesPermanent()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();

        // Act
        Func<Task> act = () => client.GetLeagueStandingsAsync(CreateQuery(), CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<UclFantasyApiException>();
        exception.Which.FailureKind.Should().Be(UclFantasyFailureKind.Permanent);
    }

    [Test]
    public async Task GetLeagueStandingsAsync_RateLimitedThenSuccess_Retries()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((attempt, _) =>
            Task.FromResult(attempt == 1
                ? new HttpResponseMessage((HttpStatusCode)429)
                : CreateJsonResponse(SinglePageJson())));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();

        // Act
        var result = await client.GetLeagueStandingsAsync(CreateQuery(), CancellationToken.None);

        // Assert
        result.Members.Should().ContainSingle();
        handler.AttemptCount.Should().Be(2);
    }

    private static UclLeagueStandingsQuery CreateQuery(int pageSize = 25, int maxPages = 10)
    {
        return new UclLeagueStandingsQuery(LeagueId, 1, 1, 2, pageSize, maxPages);
    }

    private static ServiceProvider CreateProvider(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddUclFantasyClient(CreateTestOptions())
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    private static UclFantasyClientOptions CreateTestOptions()
    {
        return new UclFantasyClientOptions
        {
            BaseAddress = new Uri("https://gaming.uefa.com/en/uclfantasy/"),
            AttemptTimeout = TimeSpan.FromMilliseconds(30),
            TotalRequestTimeout = TimeSpan.FromSeconds(5),
            MaxRetryAttempts = 3,
            RetryDelay = TimeSpan.Zero,
            UseRetryJitter = false,
            CircuitBreakerMinimumThroughput = 100,
            CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(1),
            CircuitBreakerBreakDuration = TimeSpan.FromSeconds(1)
        };
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string PageOneJson()
    {
        return """
            {
              "data": {
                "value": {
                  "leagueId": 25247707,
                  "leagueName": "Great Messi League",
                  "phaseId": 1,
                  "matchdayId": 2,
                  "totalEntries": 3,
                  "leaderboard": [
                    { "entryId": 101, "teamName": "Alpha FC", "managerName": "Anna", "rank": 1, "lastRank": 1, "total": 200, "eventTotal": 60 },
                    { "entryId": 102, "teamName": "Beta United", "managerName": "Boris", "rank": 2, "lastRank": 3, "total": 190, "eventTotal": 70 }
                  ]
                }
              }
            }
            """;
    }

    private static string PageTwoJson()
    {
        return """
            {
              "data": {
                "value": {
                  "leagueId": 25247707,
                  "leagueName": "Great Messi League",
                  "phaseId": 1,
                  "matchdayId": 2,
                  "totalEntries": 3,
                  "leaderboard": [
                    { "entryId": 103, "teamName": "Gamma City", "managerName": "Gita", "rank": 3, "lastRank": 2, "total": 180, "eventTotal": 40 }
                  ]
                }
              }
            }
            """;
    }

    private static string PageTwoDifferentTotalJson()
    {
        return """
            {
              "data": {
                "value": {
                  "leagueId": 25247707,
                  "leagueName": "Great Messi League",
                  "phaseId": 1,
                  "matchdayId": 2,
                  "totalEntries": 4,
                  "leaderboard": [
                    { "entryId": 103, "teamName": "Gamma City", "managerName": "Gita", "rank": 3, "lastRank": 2, "total": 180, "eventTotal": 40 }
                  ]
                }
              }
            }
            """;
    }

    private static string SinglePageJson()
    {
        return """
            {
              "data": {
                "value": {
                  "leagueID": 25247707,
                  "leagueName": "Great Messi League",
                  "phaseId": 1,
                  "matchdayId": 2,
                  "totalMembers": 1,
                  "leaderboard": [
                    { "entryId": 101, "teamName": "Alpha FC", "managerName": "Anna", "rank": 1, "lastRank": 1, "total": 200, "eventTotal": 60 }
                  ]
                }
              }
            }
            """;
    }

    private static string TiedRanksJson()
    {
        return """
            {
              "data": {
                "value": {
                  "leagueId": 25247707,
                  "leagueName": "Great Messi League",
                  "phaseId": 1,
                  "matchdayId": 2,
                  "totalEntries": 3,
                  "leaderboard": [
                    { "entryId": 103, "teamName": "Gamma City", "managerName": "Gita", "rank": 3, "lastRank": 3, "total": 180, "eventTotal": 40 },
                    { "entryId": 102, "teamName": "Beta United", "managerName": "Boris", "rank": 1, "lastRank": 1, "total": 200, "eventTotal": 60 },
                    { "entryId": 101, "teamName": "Alpha FC", "managerName": "Anna", "rank": 1, "lastRank": 1, "total": 200, "eventTotal": 60 }
                  ]
                }
              }
            }
            """;
    }

    private static string AlternativeNamesJson()
    {
        return """
            {
              "data": {
                "value": {
                  "leagueId": 25247707,
                  "name": "Great Messi League",
                  "phaseId": 1,
                  "matchdayId": 2,
                  "total": 1,
                  "entries": [
                    { "entry": 9001, "entryName": "Alias FC", "playerName": "Alias Manager", "position": 1, "previousRank": 2, "totalPoints": 150, "matchdayPoints": 45 }
                  ]
                }
              }
            }
            """;
    }

    private static string EmptyMembersJson()
    {
        return """
            {
              "data": {
                "value": {
                  "leagueId": 25247707,
                  "leagueName": "Great Messi League",
                  "phaseId": 1,
                  "matchdayId": 2,
                  "totalEntries": 0,
                  "leaderboard": []
                }
              }
            }
            """;
    }

    private static string MissingIdentityJson()
    {
        return """
            {
              "data": {
                "value": {
                  "leagueId": 25247707,
                  "leagueName": "Great Messi League",
                  "phaseId": 1,
                  "matchdayId": 2,
                  "totalEntries": 1,
                  "leaderboard": [
                    { "teamName": "Ghost FC", "rank": 1, "total": 100, "eventTotal": 10 }
                  ]
                }
              }
            }
            """;
    }

    private static string FullPageNoTotalJson()
    {
        return """
            {
              "data": {
                "value": {
                  "leagueId": 25247707,
                  "leagueName": "Great Messi League",
                  "phaseId": 1,
                  "matchdayId": 2,
                  "leaderboard": [
                    { "entryId": 101, "teamName": "Alpha FC", "rank": 1, "total": 200, "eventTotal": 60 },
                    { "entryId": 102, "teamName": "Beta United", "rank": 2, "total": 190, "eventTotal": 55 }
                  ]
                }
              }
            }
            """;
    }

    private static string OverlapPageOneNoTotalJson()
    {
        return """
            {
              "data": {
                "value": {
                  "leagueId": 25247707,
                  "leagueName": "Great Messi League",
                  "phaseId": 1,
                  "matchdayId": 2,
                  "leaderboard": [
                    { "entryId": 101, "teamName": "Alpha FC", "rank": 1, "total": 200, "eventTotal": 60 },
                    { "entryId": 102, "teamName": "Beta United", "rank": 2, "total": 190, "eventTotal": 55 },
                    { "entryId": 103, "teamName": "Gamma City", "rank": 3, "total": 180, "eventTotal": 40 }
                  ]
                }
              }
            }
            """;
    }

    private static string OverlapPageTwoNoTotalJson()
    {
        return """
            {
              "data": {
                "value": {
                  "leagueId": 25247707,
                  "leagueName": "Great Messi League",
                  "phaseId": 1,
                  "matchdayId": 2,
                  "leaderboard": [
                    { "entryId": 103, "teamName": "Gamma City", "rank": 3, "total": 180, "eventTotal": 40 },
                    { "entryId": 104, "teamName": "Delta Rovers", "rank": 4, "total": 170, "eventTotal": 35 }
                  ]
                }
              }
            }
            """;
    }

    private static string OverlapPageOneWithTotalJson()
    {
        return """
            {
              "data": {
                "value": {
                  "leagueId": 25247707,
                  "leagueName": "Great Messi League",
                  "phaseId": 1,
                  "matchdayId": 2,
                  "totalEntries": 3,
                  "leaderboard": [
                    { "entryId": 101, "teamName": "Alpha FC", "rank": 1, "total": 200, "eventTotal": 60 },
                    { "entryId": 102, "teamName": "Beta United", "rank": 2, "total": 190, "eventTotal": 55 }
                  ]
                }
              }
            }
            """;
    }

    private static string OverlapPageTwoWithTotalJson()
    {
        return """
            {
              "data": {
                "value": {
                  "leagueId": 25247707,
                  "leagueName": "Great Messi League",
                  "phaseId": 1,
                  "matchdayId": 2,
                  "totalEntries": 3,
                  "leaderboard": [
                    { "entryId": 102, "teamName": "Beta United", "rank": 2, "total": 190, "eventTotal": 55 },
                    { "entryId": 103, "teamName": "Gamma City", "rank": 3, "total": 180, "eventTotal": 40 }
                  ]
                }
              }
            }
            """;
    }

    private sealed class StubHttpMessageHandler(
        Func<int, HttpRequestMessage, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        private readonly List<Uri> _requestUris = [];
        private int _attemptCount;

        public int AttemptCount => _attemptCount;

        public IReadOnlyList<Uri> RequestUris => _requestUris;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _attemptCount);
            _requestUris.Add(request.RequestUri!);
            return sendAsync(attempt, request);
        }
    }
}
