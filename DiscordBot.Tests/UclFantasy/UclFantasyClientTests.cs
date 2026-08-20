using System.Net;
using System.Text;
using AwesomeAssertions;
using DiscordBot.UclFantasy;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DiscordBot.Tests.UclFantasy;

[TestFixture]
public sealed class UclFantasyClientTests
{
    [Test]
    public async Task GetWebConfigurationAsync_ValidPayload_ReturnsTourId()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(
                """
                {
                  "data": {
                    "value": {
                      "tourId": 90,
                      "FEED_BASE_URL": "https://gaming.uefa.com/en/uclfantasy/services/feeds/",
                      "fixturesUrl": "fixtures/fixtures_{{tour_id}}_{{lang}}.json"
                    }
                  },
                  "meta": { "success": true }
                }
                """)));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();

        // Act
        var result = await client.GetWebConfigurationAsync(CancellationToken.None);

        // Assert
        result.Data!.Value!.TourId.Should().Be(90);
        result.Data.Value.FixturesUrl.Should()
            .Be("fixtures/fixtures_{{tour_id}}_{{lang}}.json");
        handler.RequestUris.Should().ContainSingle(uri =>
            uri.PathAndQuery == "/en/uclfantasy/services/feeds/config/web.json");
    }

    [Test]
    public async Task GetFixturesAsync_ConfiguredFeedUrl_ReturnsMatchdayFlagsAndDeadline()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse(
                """
                {
                  "data": {
                    "value": [
                      {
                        "mdId": 1,
                        "mdIsLocked": 0,
                        "mdIsCurrent": 1,
                        "deadline": "09/08/26 06:45:00 PM",
                        "match": [
                          {
                            "mId": -1,
                            "htName": "TBC",
                            "atName": "TBC"
                          }
                        ]
                      }
                    ]
                  },
                  "meta": { "success": true }
                }
                """)));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();
        var configuration = new UclFantasyWebConfigurationValue
        {
            TourId = 90,
            FeedBaseUrl = "https://gaming.uefa.com/en/uclfantasy/services/feeds/",
            FixturesUrl = "fixtures/fixtures_{{tour_id}}_{{lang}}.json"
        };

        // Act
        var result = await client.GetFixturesAsync(
            configuration,
            CancellationToken.None);

        // Assert
        var matchday = result.Data!.Value!.Should().ContainSingle().Subject;
        matchday.MatchdayId.Should().Be(1);
        matchday.IsLocked.Should().Be(0);
        matchday.IsCurrent.Should().Be(1);
        matchday.Deadline.Should().Be("09/08/26 06:45:00 PM");
        matchday.Matches.Should().ContainSingle();
        handler.RequestUris.Should().ContainSingle(uri =>
            uri.PathAndQuery == "/en/uclfantasy/services/feeds/fixtures/fixtures_90_en.json");
    }

    [Test]
    public async Task GetWebConfigurationAsync_TransientFailureThenSuccess_Retries()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((attempt, _) =>
            Task.FromResult(attempt == 1
                ? CreateResponse(HttpStatusCode.ServiceUnavailable)
                : CreateJsonResponse(
                    """
                    {
                      "data": { "value": { "tourId": 90 } }
                    }
                    """)));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();

        // Act
        var result = await client.GetWebConfigurationAsync(CancellationToken.None);

        // Assert
        result.Data!.Value!.TourId.Should().Be(90);
        handler.AttemptCount.Should().Be(2);
    }

    [Test]
    public async Task GetFixturesAsync_MalformedPayload_ClassifiesInvalidPayload()
    {
        // Arrange
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateJsonResponse("{\"data\": {}}")));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IUclFantasyClient>();
        var configuration = new UclFantasyWebConfigurationValue
        {
            TourId = 90
        };

        // Act
        Func<Task> act = () => client.GetFixturesAsync(
            configuration,
            CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<UclFantasyApiException>();
        exception.Which.FailureKind.Should().Be(UclFantasyFailureKind.InvalidPayload);
        handler.AttemptCount.Should().Be(1);
    }

    private static ServiceProvider CreateProvider(
        HttpMessageHandler handler,
        UclFantasyClientOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddUclFantasyClient(options ?? CreateTestOptions())
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    private static UclFantasyClientOptions CreateTestOptions()
    {
        return new UclFantasyClientOptions
        {
            BaseAddress = new Uri("https://gaming.uefa.com/en/uclfantasy/"),
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
