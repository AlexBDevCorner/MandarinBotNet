using System.Net;
using AwesomeAssertions;
using DiscordBot.EventWatch;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DiscordBot.Tests.EventWatch;

[TestFixture]
public sealed class RigaFcClientTests
{
    [Test]
    public async Task GetPageAsync_Success_SendsUserAgentAndAcceptAndReturnsHtml()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body>ok</body></html>")
            });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://rigafc.lv/")
        };
        var client = new RigaFcClient(httpClient, new RecordingLogger<RigaFcClient>());

        var html = await client.GetPageAsync(
            new Uri("https://rigafc.lv/"),
            CancellationToken.None);

        html.Should().Contain("ok");
        handler.LastRequest.Should().NotBeNull();
        var userAgent = string.Join(" ", handler.LastRequest!.Headers.UserAgent.ToString());
        userAgent.Should().Contain("MandarinBotNet");
        handler.LastRequest.Headers.Accept.Should().ContainSingle(a => a.MediaType == "text/html");
    }

    [Test]
    public async Task GetPageAsync_NonSuccess_ThrowsObservably()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://rigafc.lv/")
        };
        var client = new RigaFcClient(httpClient, new RecordingLogger<RigaFcClient>());

        Func<Task> act = () => client.GetPageAsync(
            new Uri("https://rigafc.lv/kalendars/"),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<RigaFcApiException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        exception.Which.Message.Should().Contain("kalendars");
    }

    [Test]
    public void GetPageAsync_Cancelled_ThrowsCancellation()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("html")
            });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://rigafc.lv/")
        };
        var client = new RigaFcClient(httpClient, new RecordingLogger<RigaFcClient>());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => client.GetPageAsync(
            new Uri("https://rigafc.lv/"),
            cts.Token);

        act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public void SourceIdentifier_IsStableWithoutTime()
    {
        var first = EventWatchSourceIdentifier.Create("riga-fc-atalanta-2026");
        var second = EventWatchSourceIdentifier.Create("riga-fc-atalanta-2026");

        first.Should().Be("event-watch:riga-fc-atalanta-2026");
        second.Should().Be(first);
        first.Should().NotContain(DateTime.UtcNow.ToString("HH:mm:ss"));
        first.Should().NotContain("polling");
    }

    [Test]
    public void PageUris_ContainsThreeOfficialPagesOnly()
    {
        RigaFcClient.PageUris.Should().HaveCount(3);
        RigaFcClient.PageUris.Should().Contain(new Uri("https://rigafc.lv/"));
        RigaFcClient.PageUris.Should().Contain(new Uri("https://rigafc.lv/kalendars/"));
        RigaFcClient.PageUris.Should().Contain(new Uri("https://rigafc.lv/jaunumi/"));
        RigaFcClient.PageUris.Should().OnlyContain(uri => uri.Host == "rigafc.lv");
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response);
        }
    }
}
