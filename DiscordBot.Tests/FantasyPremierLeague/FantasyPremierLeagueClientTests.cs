using System.Net;
using System.Text;
using AwesomeAssertions;
using DiscordBot.FantasyPremierLeague;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DiscordBot.Tests.FantasyPremierLeague;

[TestFixture]
public sealed class FantasyPremierLeagueClientTests
{
    [Test]
    public async Task GetBootstrapStaticAsync_TooManyRequestsThenSuccess_RetriesAndReturnsPayload()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((attempt, _) =>
            Task.FromResult(attempt == 1
                ? CreateResponse(HttpStatusCode.TooManyRequests)
                : CreateJsonResponse(
                    """
                    {
                      "events": [
                        {
                          "id": 42,
                          "is_next": true,
                          "deadline_time_epoch": 1770000000
                        }
                      ]
                    }
                    """)));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IFantasyPremierLeagueClient>();

        // Act
        var result = await client.GetBootstrapStaticAsync(CancellationToken.None);

        // Assert
        result.Events.Should().ContainSingle();
        result.Events[0].Id.Should().Be(42);
        handler.AttemptCount.Should().Be(2);
        handler.RequestUris.Should().OnlyContain(
            uri => uri.AbsolutePath == "/api/bootstrap-static/");
    }

    [Test]
    public async Task GetClassicStandingsAsync_UtcLastUpdated_DeserializesAsDateTimeOffset()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(
                """
                {
                  "standings": { "results": [] },
                  "last_updated_data": "2027-02-02T12:00:00+00:00"
                }
                """)));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IFantasyPremierLeagueClient>();

        // Act
        var result = await client.GetClassicStandingsAsync(
            1671531,
            CancellationToken.None);

        // Assert
        result.LastUpdatedData.Should().Be(
            new DateTimeOffset(2027, 2, 2, 12, 0, 0, TimeSpan.Zero));
    }

    [Test]
    public async Task GetClassicStandingsAsync_MultiplePages_AggregatesEveryPageInOrder()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((attempt, _) =>
            Task.FromResult(CreateJsonResponse(attempt switch
            {
                1 => ClassicStandingsPage(page: 1, hasNext: true, rank: 1),
                2 => ClassicStandingsPage(page: 2, hasNext: true, rank: 51),
                3 => ClassicStandingsPage(page: 3, hasNext: false, rank: 101),
                _ => throw new InvalidOperationException("Unexpected page request.")
            })));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IFantasyPremierLeagueClient>();

        // Act
        var result = await client.GetClassicStandingsAsync(
            1671531,
            CancellationToken.None);

        // Assert
        result.Standings.Results.Select(standing => standing.Rank).Should()
            .BeEquivalentTo([1, 51, 101], options => options.WithStrictOrdering());
        handler.RequestUris.Select(uri => uri.PathAndQuery).Should()
            .BeEquivalentTo(
                [
                    "/api/leagues-classic/1671531/standings/",
                    "/api/leagues-classic/1671531/standings/?page_standings=2",
                    "/api/leagues-classic/1671531/standings/?page_standings=3"
                ],
                options => options.WithStrictOrdering());
    }

    [Test]
    public async Task GetHeadToHeadStandingsAsync_MorePagesThanConfigured_StopsAtLimit()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((attempt, _) =>
            Task.FromResult(CreateJsonResponse(
                HeadToHeadStandingsPage(
                    page: attempt,
                    hasNext: true,
                    rank: attempt))));
        using var provider = CreateProvider(
            handler,
            leagueOptions: new FantasyPremierLeagueOptions
            {
                MaxStandingsPages = 2
            });
        var client = provider.GetRequiredService<IFantasyPremierLeagueClient>();

        // Act
        var result = await client.GetHeadToHeadStandingsAsync(
            7654321,
            CancellationToken.None);

        // Assert
        result.HeadToHeadStandings.Results.Select(standing => standing.Rank).Should()
            .BeEquivalentTo([1, 2], options => options.WithStrictOrdering());
        handler.RequestUris.Select(uri => uri.PathAndQuery).Should()
            .BeEquivalentTo(
                [
                    "/api/leagues-h2h/7654321/standings/",
                    "/api/leagues-h2h/7654321/standings/?page_standings=2"
                ],
                options => options.WithStrictOrdering());
    }

    [Test]
    public async Task GetBootstrapStaticAsync_ServerErrorsExhausted_ClassifiesTransientFailure()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateResponse(HttpStatusCode.ServiceUnavailable)));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IFantasyPremierLeagueClient>();

        // Act
        Func<Task> act = () => client.GetBootstrapStaticAsync(CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<FantasyPremierLeagueApiException>();
        exception.Which.FailureKind.Should().Be(FantasyPremierLeagueFailureKind.Transient);
        exception.Which.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        handler.AttemptCount.Should().Be(3);
    }

    [Test]
    public async Task GetBootstrapStaticAsync_AttemptTimesOut_RetriesAndClassifiesTransientFailure()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CreateResponse(HttpStatusCode.OK);
        });
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IFantasyPremierLeagueClient>();

        // Act
        Func<Task> act = () => client.GetBootstrapStaticAsync(CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<FantasyPremierLeagueApiException>();
        exception.Which.FailureKind.Should().Be(FantasyPremierLeagueFailureKind.Transient);
        handler.AttemptCount.Should().Be(3);
    }

    [Test]
    public async Task GetBootstrapStaticAsync_CallerCancels_PropagatesWithoutRetrying()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CreateResponse(HttpStatusCode.OK);
        });
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IFantasyPremierLeagueClient>();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(TimeSpan.FromMilliseconds(20));

        // Act
        Func<Task> act = () => client.GetBootstrapStaticAsync(cancellationSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.AttemptCount.Should().Be(1);
    }

    [Test]
    public async Task GetBootstrapStaticAsync_MalformedJson_ClassifiesInvalidPayloadWithoutRetrying()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse("""{"events": [""")));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IFantasyPremierLeagueClient>();

        // Act
        Func<Task> act = () => client.GetBootstrapStaticAsync(CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<FantasyPremierLeagueApiException>();
        exception.Which.FailureKind.Should().Be(FantasyPremierLeagueFailureKind.InvalidPayload);
        handler.AttemptCount.Should().Be(1);
    }

    [Test]
    public async Task GetBootstrapStaticAsync_NotFound_ClassifiesPermanentFailureWithoutRetrying()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateResponse(HttpStatusCode.NotFound)));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IFantasyPremierLeagueClient>();

        // Act
        Func<Task> act = () => client.GetBootstrapStaticAsync(CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<FantasyPremierLeagueApiException>();
        exception.Which.FailureKind.Should().Be(FantasyPremierLeagueFailureKind.Permanent);
        exception.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        handler.AttemptCount.Should().Be(1);
    }

    [Test]
    public async Task GetBootstrapStaticAsync_CircuitOpens_ClassifiesOpenCircuitAsTransient()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateResponse(HttpStatusCode.ServiceUnavailable)));
        using var provider = CreateProvider(
            handler,
            new FantasyPremierLeagueClientOptions
            {
                AttemptTimeout = TimeSpan.FromMilliseconds(30),
                TotalRequestTimeout = TimeSpan.FromSeconds(1),
                MaxRetryAttempts = 1,
                RetryDelay = TimeSpan.Zero,
                UseRetryJitter = false,
                CircuitBreakerFailureRatio = 1,
                CircuitBreakerMinimumThroughput = 2,
                CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(1),
                CircuitBreakerBreakDuration = TimeSpan.FromSeconds(1)
            });
        var client = provider.GetRequiredService<IFantasyPremierLeagueClient>();

        // Act
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Func<Task> failedRequest = () =>
                client.GetBootstrapStaticAsync(CancellationToken.None);
            await failedRequest.Should().ThrowAsync<FantasyPremierLeagueApiException>();
        }

        Func<Task> openCircuitRequest = () =>
            client.GetBootstrapStaticAsync(CancellationToken.None);

        // Assert
        var exception = await openCircuitRequest
            .Should()
            .ThrowAsync<FantasyPremierLeagueApiException>();
        exception.Which.FailureKind.Should().Be(FantasyPremierLeagueFailureKind.Transient);
        handler.AttemptCount.Should().Be(2);
    }

    [Test]
    public void ClientOptions_Defaults_BoundRetriesAndEnableJitter()
    {
        // Arrange
        var options = new FantasyPremierLeagueClientOptions();

        // Act
        var retryAttempts = options.MaxRetryAttempts;

        // Assert
        retryAttempts.Should().Be(2);
        options.RetryDelay.Should().BePositive();
        options.UseRetryJitter.Should().BeTrue();
        options.TotalRequestTimeout.Should().BeGreaterThan(options.AttemptTimeout);
    }

    private static ServiceProvider CreateProvider(
        HttpMessageHandler handler,
        FantasyPremierLeagueClientOptions? options = null,
        FantasyPremierLeagueOptions? leagueOptions = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(leagueOptions ?? new FantasyPremierLeagueOptions());
        services
            .AddFantasyPremierLeagueClient(options ?? CreateTestOptions())
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    private static FantasyPremierLeagueClientOptions CreateTestOptions()
    {
        return new FantasyPremierLeagueClientOptions
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(30),
            TotalRequestTimeout = TimeSpan.FromSeconds(1),
            MaxRetryAttempts = 2,
            RetryDelay = TimeSpan.Zero,
            UseRetryJitter = false,
            CircuitBreakerMinimumThroughput = 100,
            CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(1),
            CircuitBreakerBreakDuration = TimeSpan.FromSeconds(1)
        };
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode)
    {
        return new HttpResponseMessage(statusCode);
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string ClassicStandingsPage(
        int page,
        bool hasNext,
        int rank)
    {
        return $$"""
            {
              "standings": {
                "page": {{page}},
                "has_next": {{hasNext.ToString().ToLowerInvariant()}},
                "results": [
                  {
                    "rank": {{rank}},
                    "entry_name": "Team {{rank}}"
                  }
                ]
              },
              "last_updated_data": "2027-02-02T12:00:00+00:00"
            }
            """;
    }

    private static string HeadToHeadStandingsPage(
        int page,
        bool hasNext,
        int rank)
    {
        return $$"""
            {
              "standings": {
                "page": {{page}},
                "has_next": {{hasNext.ToString().ToLowerInvariant()}},
                "results": [
                  {
                    "rank": {{rank}},
                    "entry_name": "Team {{rank}}"
                  }
                ]
              }
            }
            """;
    }

    private sealed class StubHttpMessageHandler(
        Func<int, CancellationToken, Task<HttpResponseMessage>> sendAsync)
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
            return sendAsync(attempt, cancellationToken);
        }
    }
}
