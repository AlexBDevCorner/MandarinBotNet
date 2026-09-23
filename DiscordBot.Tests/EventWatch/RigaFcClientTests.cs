using System.Globalization;
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
    public async Task GetPageAsync_Success_SendsUserAgentAndAcceptAndReturnsPayload()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"success\",\"items\":[]}")
            });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.bilesuserviss.lv/")
        };
        var client = new RigaFcClient(httpClient, new RecordingLogger<RigaFcClient>());

        var payload = await client.GetPageAsync(
            RigaFcClient.TicketCatalogueApiUri,
            CancellationToken.None);

        payload.Should().Contain("success");
        handler.LastRequest.Should().NotBeNull();
        var userAgent = string.Join(" ", handler.LastRequest!.Headers.UserAgent.ToString());
        userAgent.Should().Contain("MandarinBotNet");
        handler.LastRequest.Headers.Accept.Should().ContainSingle(a => a.MediaType == "application/json");
    }

    [Test]
    public async Task GetTicketCatalogueAsync_RequestsCatalogueApi()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"success\",\"items\":[]}")
            });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.bilesuserviss.lv/")
        };
        var client = new RigaFcClient(httpClient, new RecordingLogger<RigaFcClient>());

        await client.GetTicketCatalogueAsync(CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri.Should().Be(RigaFcClient.TicketCatalogueApiUri);
    }

    [Test]
    public async Task GetPageAsync_NonSuccess_ThrowsObservably()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.bilesuserviss.lv/")
        };
        var client = new RigaFcClient(httpClient, new RecordingLogger<RigaFcClient>());

        Func<Task> act = () => client.GetPageAsync(
            RigaFcClient.TicketCatalogueApiUri,
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<RigaFcApiException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        exception.Which.Message.Should().Contain("bilesuserviss.lv");
    }

    [Test]
    public async Task GetPageAsync_Cancelled_ThrowsCancellation()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("payload")
            });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.bilesuserviss.lv/")
        };
        var client = new RigaFcClient(httpClient, new RecordingLogger<RigaFcClient>());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => client.GetPageAsync(
            RigaFcClient.TicketCatalogueApiUri,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public void SourceIdentifier_IsStableWithoutTime()
    {
        var first = EventWatchSourceIdentifier.Create("riga-fc-atalanta-2026");
        var second = EventWatchSourceIdentifier.Create("riga-fc-atalanta-2026");

        first.Should().Be("event-watch:riga-fc-atalanta-2026");
        second.Should().Be(first);
        first.Should().NotContain(DateTime.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        first.Should().NotContain("polling");
    }

    [Test]
    public void PageUris_ContainsOnlyTicketCatalogue()
    {
        // Calendar-style generic Tickets content cannot trigger because the
        // homepage, calendar, and news pages are no longer EventWatch sources.
        RigaFcClient.PageUris.Should().ContainSingle()
            .Which.Should().Be(RigaFcClient.TicketCatalogueApiUri);
        RigaFcClient.PageUris.Should().OnlyContain(uri => uri.Host == "www.bilesuserviss.lv");
        RigaFcClient.PageUris.Should().NotContain(new Uri("https://rigafc.lv/"));
        RigaFcClient.PageUris.Should().NotContain(new Uri("https://rigafc.lv/kalendars/"));
        RigaFcClient.PageUris.Should().NotContain(new Uri("https://rigafc.lv/jaunumi/"));
    }

    [Test]
    public void CatalogueUris_DocumentShopCatalogueAndPublicLandingPage()
    {
        RigaFcClient.TicketCatalogueUri.Should().Be(new Uri("https://shop.rigafc.lv/tickets"));
        RigaFcClient.TicketLandingUri.Should().Be(new Uri("https://rigafc.lv/biletes/"));
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
