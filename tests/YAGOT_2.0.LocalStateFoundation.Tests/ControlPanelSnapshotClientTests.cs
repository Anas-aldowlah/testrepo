using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Integration.SiteState;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class ControlPanelSnapshotClientTests
{
    private const string ApiKey = "snapshot-client-test-key";

    [Fact]
    public async Task ValidResponse_UsesExactAuthenticatedGetContract()
    {
        HttpRequestMessage? captured = null;
        var handler = new DelegateHandler(request =>
        {
            captured = request;
            return Task.FromResult(ValidResponse());
        });
        var client = CreateClient(handler);

        var result = await client.GetSnapshotAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Body);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Get, captured.Method);
        Assert.Equal(
            "https://control-panel.test/api/v1/sites/1/snapshot",
            captured.RequestUri!.AbsoluteUri);
        Assert.Equal(
            [ApiKey],
            captured.Headers.GetValues(
                ControlPanelSnapshotClient.ApiKeyHeaderName));
        Assert.Equal(
            ["application/json"],
            captured.Headers.Accept.Select(value => value.MediaType));
        Assert.True(captured.Headers.CacheControl!.NoCache);
        Assert.True(captured.Headers.CacheControl.NoStore);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ControlPanelSnapshotFailure.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, ControlPanelSnapshotFailure.Forbidden, false)]
    [InlineData(HttpStatusCode.NotFound, ControlPanelSnapshotFailure.NotFound, false)]
    [InlineData(HttpStatusCode.TooManyRequests, ControlPanelSnapshotFailure.Throttled, true)]
    [InlineData(HttpStatusCode.InternalServerError, ControlPanelSnapshotFailure.ServerError, true)]
    [InlineData(HttpStatusCode.BadGateway, ControlPanelSnapshotFailure.ServerError, true)]
    [InlineData(HttpStatusCode.Redirect, ControlPanelSnapshotFailure.Protocol, false)]
    public async Task Non200_IsClassifiedWithoutReadingBody(
        HttpStatusCode statusCode,
        ControlPanelSnapshotFailure expected,
        bool retryable)
    {
        var content = new ThrowingContent();
        var handler = new DelegateHandler(_ => Task.FromResult(
            new HttpResponseMessage(statusCode) { Content = content }));

        var result = await CreateClient(handler)
            .GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(expected, result.Failure);
        Assert.Equal(retryable, result.IsRetryable);
        Assert.False(content.WasRead);
    }

    [Fact]
    public async Task Throttled_ParsesRetryAfter()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(
            TimeSpan.FromSeconds(45));
        var handler = new DelegateHandler(_ => Task.FromResult(response));

        var result = await CreateClient(handler)
            .GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(45), result.RetryAfter);
    }

    [Fact]
    public async Task Valid200_RequiresNoStoreAndUtf8Json()
    {
        var missingNoStore = ValidResponse();
        missingNoStore.Headers.CacheControl = null;
        var wrongMedia = ValidResponse();
        wrongMedia.Content.Headers.ContentType =
            new MediaTypeHeaderValue("text/plain");
        var wrongCharset = ValidResponse();
        wrongCharset.Content.Headers.ContentType!.CharSet = "utf-16";
        var responses = new Queue<HttpResponseMessage>(
            [missingNoStore, wrongMedia, wrongCharset]);
        var client = CreateClient(new DelegateHandler(
            _ => Task.FromResult(responses.Dequeue())));

        Assert.Equal(
            ControlPanelSnapshotFailure.MissingNoStore,
            (await client.GetSnapshotAsync(CancellationToken.None)).Failure);
        Assert.Equal(
            ControlPanelSnapshotFailure.InvalidMediaType,
            (await client.GetSnapshotAsync(CancellationToken.None)).Failure);
        Assert.Equal(
            ControlPanelSnapshotFailure.InvalidMediaType,
            (await client.GetSnapshotAsync(CancellationToken.None)).Failure);
    }

    [Fact]
    public async Task OversizedDeclaredAndStreamingBodies_AreRejected()
    {
        var declared = ValidResponse();
        declared.Content = new ByteArrayContent(
            new byte[SiteStateReconciliationOptions.ResponseSizeLimitBytes + 1]);
        declared.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/json");
        var streamed = ValidResponse();
        streamed.Content = new UnknownLengthContent(
            new byte[SiteStateReconciliationOptions.ResponseSizeLimitBytes + 1]);
        streamed.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/json");
        var responses = new Queue<HttpResponseMessage>([declared, streamed]);
        var client = CreateClient(new DelegateHandler(
            _ => Task.FromResult(responses.Dequeue())));

        Assert.Equal(
            ControlPanelSnapshotFailure.ResponseTooLarge,
            (await client.GetSnapshotAsync(CancellationToken.None)).Failure);
        Assert.Equal(
            ControlPanelSnapshotFailure.ResponseTooLarge,
            (await client.GetSnapshotAsync(CancellationToken.None)).Failure);
    }

    [Fact]
    public async Task NetworkAndTimeout_AreSanitizedRetryableFailures()
    {
        var network = CreateClient(new DelegateHandler(
            _ => throw new HttpRequestException("contains-sensitive-uri")));
        var timeout = CreateClient(
            new DelegateHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException();
            }),
            timeoutSeconds: 0);

        var networkResult = await network.GetSnapshotAsync(CancellationToken.None);
        var timeoutResult = await timeout.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(ControlPanelSnapshotFailure.Network, networkResult.Failure);
        Assert.Equal(ControlPanelSnapshotFailure.Timeout, timeoutResult.Failure);
        Assert.True(networkResult.IsRetryable);
        Assert.True(timeoutResult.IsRetryable);
    }

    [Fact]
    public async Task CallerCancellation_IsPropagated()
    {
        var client = CreateClient(new DelegateHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException();
        }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetSnapshotAsync(cancellation.Token));
    }

    private static ControlPanelSnapshotClient CreateClient(
        HttpMessageHandler handler,
        int timeoutSeconds = 10)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://control-panel.test/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
        return new ControlPanelSnapshotClient(
            httpClient,
            Options.Create(new SiteStateReconciliationOptions
            {
                SiteId = 1,
                ControlPanelBaseUrl = "https://control-panel.test/",
                SnapshotApiKey = ApiKey,
                ReconciliationIntervalMinutes = 30,
                SnapshotHttpTimeoutSeconds = timeoutSeconds
            }),
            TimeProvider.System);
    }

    private static HttpResponseMessage ValidResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("{}"))
        };
        response.Headers.CacheControl = new CacheControlHeaderValue
        {
            NoStore = true
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(
            "application/json")
        {
            CharSet = "utf-8"
        };
        return response;
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken,
            Task<HttpResponseMessage>> _send;

        public DelegateHandler(
            Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
            : this((request, _) => send(request))
        {
        }

        public DelegateHandler(
            Func<HttpRequestMessage, CancellationToken,
                Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _send(request, cancellationToken);
    }

    private sealed class ThrowingContent : HttpContent
    {
        public bool WasRead { get; private set; }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            WasRead = true;
            throw new InvalidOperationException("Non-200 body must not be read.");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
