using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Controllers.Api;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateWebhookEndpointTests
{
    [Fact]
    [Trait("Suite", "YAGOT02Endpoint")]
    public async Task ValidRequest_ReachesApplyOnce_WithExactBodyHash()
    {
        var apply = new CapturingApplyService();
        await using var host = await WebhookTestHost.CreateAsync(apply);
        var body = ValidBody();

        using var response = await host.SendSignedAsync(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(1, apply.CallCount);
        Assert.Equal(SiteStateWebhookAuthenticationTests.DeliveryId,
            apply.Delivery!.DeliveryId);
        Assert.Equal(SHA256.HashData(body), apply.Delivery.PayloadSha256);
        Assert.Equal(0, host.LegacyStatusCallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT02Endpoint")]
    public async Task InvalidHmac_DoesNotParseOrApply()
    {
        var apply = new CapturingApplyService();
        var parser = new CountingParser();
        await using var host = await WebhookTestHost.CreateAsync(apply, parser);
        var body = ValidBody();
        using var request = WebhookTestHost.SignedRequest(body);
        request.Headers.Remove(SiteStateWebhookAuthenticator.SignatureHeaderName);
        request.Headers.TryAddWithoutValidation(
            SiteStateWebhookAuthenticator.SignatureHeaderName,
            "sha256=" + new string('0', 64));

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, parser.CallCount);
        Assert.Equal(0, apply.CallCount);
    }

    [Theory]
    [InlineData("missing-header", HttpStatusCode.BadRequest)]
    [InlineData("duplicate-header", HttpStatusCode.BadRequest)]
    [InlineData("malformed-timestamp", HttpStatusCode.BadRequest)]
    [InlineData("expired-timestamp", HttpStatusCode.Unauthorized)]
    [InlineData("future-timestamp", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-key", HttpStatusCode.Unauthorized)]
    [InlineData("malformed-signature", HttpStatusCode.BadRequest)]
    [InlineData("invalid-hmac", HttpStatusCode.Unauthorized)]
    [InlineData("malformed-delivery", HttpStatusCode.BadRequest)]
    [Trait("Suite", "YAGOT02Endpoint")]
    public async Task EnvelopeFailure_IsMappedToFrozenStatus(
        string variation,
        HttpStatusCode expected)
    {
        await using var host = await WebhookTestHost.CreateAsync(
            new CapturingApplyService());
        var body = ValidBody();
        using var request = WebhookTestHost.SignedRequest(body);

        switch (variation)
        {
            case "missing-header":
                request.Headers.Remove(
                    SiteStateWebhookAuthenticator.KeyIdHeaderName);
                break;
            case "duplicate-header":
                request.Headers.Remove(
                    SiteStateWebhookAuthenticator.KeyIdHeaderName);
                request.Headers.TryAddWithoutValidation(
                    SiteStateWebhookAuthenticator.KeyIdHeaderName,
                    new[] { SiteStateWebhookAuthenticationTests.TestKeyId, "second" });
                break;
            case "malformed-timestamp":
                ReplaceHeader(request,
                    SiteStateWebhookAuthenticator.TimestampHeaderName,
                    "+1788516000");
                break;
            case "expired-timestamp":
                ReplaceHeader(request,
                    SiteStateWebhookAuthenticator.TimestampHeaderName,
                    (SiteStateWebhookAuthenticationTests.NowUnixSeconds - 301)
                    .ToString());
                break;
            case "future-timestamp":
                ReplaceHeader(request,
                    SiteStateWebhookAuthenticator.TimestampHeaderName,
                    (SiteStateWebhookAuthenticationTests.NowUnixSeconds + 301)
                    .ToString());
                break;
            case "wrong-key":
                ReplaceHeader(request,
                    SiteStateWebhookAuthenticator.KeyIdHeaderName,
                    "wrong-key");
                break;
            case "malformed-signature":
                ReplaceHeader(request,
                    SiteStateWebhookAuthenticator.SignatureHeaderName,
                    "sha256=abcd");
                break;
            case "invalid-hmac":
                ReplaceHeader(request,
                    SiteStateWebhookAuthenticator.SignatureHeaderName,
                    "sha256=" + new string('0', 64));
                break;
            case "malformed-delivery":
                ReplaceHeader(request,
                    SiteStateWebhookAuthenticator.DeliveryIdHeaderName,
                    "00112233-4455-6677-8899-AABBCCDDEEFF");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(variation));
        }

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    [Trait("Suite", "YAGOT02Endpoint")]
    public async Task WrongMethod_ReturnsStable405WithoutRedirect()
    {
        await using var host = await WebhookTestHost.CreateAsync(
            new CapturingApplyService());

        using var response = await host.Client.GetAsync(SiteStateWebhookRoute.Path);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(
            "POST",
            response.Content.Headers.Allow.Single());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("method_not_allowed", content, StringComparison.Ordinal);
        Assert.Null(response.Headers.Location);
        Assert.Equal(0, host.LegacyStatusCallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT02Endpoint")]
    public async Task UnsupportedMediaTypeOrEncoding_Returns415()
    {
        await using var host = await WebhookTestHost.CreateAsync(
            new CapturingApplyService());
        var body = ValidBody();

        using var wrongType = WebhookTestHost.SignedRequest(body);
        wrongType.Content!.Headers.ContentType =
            MediaTypeHeaderValue.Parse("text/plain; charset=utf-8");
        using var wrongTypeResponse = await host.Client.SendAsync(wrongType);

        using var wrongEncoding = WebhookTestHost.SignedRequest(body);
        wrongEncoding.Content!.Headers.ContentEncoding.Add("gzip");
        using var wrongEncodingResponse = await host.Client.SendAsync(wrongEncoding);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType,
            wrongTypeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType,
            wrongEncodingResponse.StatusCode);
    }

    [Fact]
    [Trait("Suite", "YAGOT02Endpoint")]
    public async Task StandardsCompliantJsonMediaTypeCasing_IsAccepted()
    {
        await using var host = await WebhookTestHost.CreateAsync(
            new CapturingApplyService());
        using var request = WebhookTestHost.SignedRequest(ValidBody());
        request.Content!.Headers.ContentType =
            MediaTypeHeaderValue.Parse("Application/JSON ; Charset=\"UTF-8\"");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Suite", "YAGOT02Endpoint")]
    public async Task ContentLengthAndStreamingBodiesOverLimit_Return413()
    {
        await using var host = await WebhookTestHost.CreateAsync(
            new CapturingApplyService());
        var oversized = new byte[SiteStateWebhookOptions.BodySizeLimitBytes + 1];

        using var lengthRequest = WebhookTestHost.SignedRequest(oversized);
        using var lengthResponse = await host.Client.SendAsync(lengthRequest);

        using var streamingRequest = WebhookTestHost.SignedRequest(
            oversized,
            new StreamingContent(oversized));
        using var streamingResponse = await host.Client.SendAsync(streamingRequest);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            lengthResponse.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            streamingResponse.StatusCode);
    }

    [Theory]
    [InlineData("{", HttpStatusCode.BadRequest)]
    [InlineData("wrong-version", HttpStatusCode.UnprocessableEntity)]
    [InlineData("wrong-site", HttpStatusCode.Forbidden)]
    [InlineData("invalid-mode", HttpStatusCode.UnprocessableEntity)]
    [InlineData("missing-property", HttpStatusCode.UnprocessableEntity)]
    [InlineData("duplicate-property", HttpStatusCode.UnprocessableEntity)]
    [InlineData("unknown-property", HttpStatusCode.UnprocessableEntity)]
    [InlineData("quoted-contract-version", HttpStatusCode.UnprocessableEntity)]
    [InlineData("quoted-site-id", HttpStatusCode.UnprocessableEntity)]
    [InlineData("quoted-revision", HttpStatusCode.UnprocessableEntity)]
    [InlineData("quoted-duration", HttpStatusCode.UnprocessableEntity)]
    [InlineData("numeric-mode", HttpStatusCode.UnprocessableEntity)]
    [InlineData("boolean-site-name", HttpStatusCode.UnprocessableEntity)]
    [InlineData("numeric-effective-at", HttpStatusCode.UnprocessableEntity)]
    [InlineData("numeric-start-date", HttpStatusCode.UnprocessableEntity)]
    [Trait("Suite", "YAGOT02Endpoint")]
    public async Task AuthenticatedInvalidJsonOrContract_ReturnsExpectedStatus(
        string variation,
        HttpStatusCode expected)
    {
        await using var host = await WebhookTestHost.CreateAsync(
            new CapturingApplyService());
        var body = variation == "{"
            ? Encoding.UTF8.GetBytes(variation)
            : InvalidBody(variation);

        using var response = await host.SendSignedAsync(body);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(SiteStateApplyOutcome.Applied, null, HttpStatusCode.OK)]
    [InlineData(SiteStateApplyOutcome.Equal, null, HttpStatusCode.OK)]
    [InlineData(SiteStateApplyOutcome.Stale, null, HttpStatusCode.OK)]
    [InlineData(SiteStateApplyOutcome.DuplicateDelivery,
        SiteStateReceiptDecision.Applied, HttpStatusCode.OK)]
    [InlineData(SiteStateApplyOutcome.EqualConflict, null,
        HttpStatusCode.Conflict)]
    [InlineData(SiteStateApplyOutcome.DuplicateDelivery,
        SiteStateReceiptDecision.EqualConflict, HttpStatusCode.Conflict)]
    [InlineData(SiteStateApplyOutcome.DeliveryIdPayloadConflict, null,
        HttpStatusCode.Conflict)]
    [Trait("Suite", "YAGOT02Endpoint")]
    public async Task DurableOutcome_IsMappedToFrozenStatus(
        SiteStateApplyOutcome outcome,
        SiteStateReceiptDecision? originalDecision,
        HttpStatusCode expected)
    {
        var apply = new CapturingApplyService
        {
            Result = new SiteStateApplyResult(outcome, originalDecision)
        };
        await using var host = await WebhookTestHost.CreateAsync(apply);

        using var response = await host.SendSignedAsync(ValidBody());

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.ServiceUnavailable,
        "temporary_storage_failure")]
    [InlineData(false, HttpStatusCode.InternalServerError, "server_error")]
    [Trait("Suite", "YAGOT02Endpoint")]
    public async Task ApplyFailure_IsSanitized(
        bool temporary,
        HttpStatusCode expected,
        string expectedCode)
    {
        const string privateMessage = "private-sql-detail-marker";
        var apply = new CapturingApplyService
        {
            Exception = temporary
                ? new TimeoutException(privateMessage)
                : new InvalidOperationException(privateMessage)
        };
        await using var host = await WebhookTestHost.CreateAsync(apply);

        using var response = await host.SendSignedAsync(ValidBody());
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(expected, response.StatusCode);
        Assert.Contains(expectedCode, content, StringComparison.Ordinal);
        Assert.DoesNotContain(privateMessage, content, StringComparison.Ordinal);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    [Trait("Suite", "YAGOT02Endpoint")]
    public async Task RequestCancellation_IsPropagatedToApplyService()
    {
        var apply = new CancellationAwareApplyService();
        await using var host = await WebhookTestHost.CreateAsync(apply);
        using var request = WebhookTestHost.SignedRequest(ValidBody());
        using var cancellation = new CancellationTokenSource();

        var send = host.Client.SendAsync(request, cancellation.Token);
        await apply.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
        Assert.True(apply.ObservedCancellation);
    }

    [Fact]
    [Trait("Suite", "YAGOT02Endpoint")]
    public async Task LogsNeverContainSecretSignatureOrRawBodyMarkers()
    {
        const string secretMarker =
            "secret-log-marker-0123456789abcdef0123456789";
        const string rawBodyMarker = "raw-body-log-marker";
        const string signatureMarker = "signature-log-marker";
        var logs = new CapturingLogProvider();
        await using var host = await WebhookTestHost.CreateAsync(
            new CapturingApplyService(),
            logs: logs,
            secret: secretMarker);

        var body = Encoding.UTF8.GetBytes("{" + rawBodyMarker);
        using var malformedJson = WebhookTestHost.SignedRequest(
            body,
            secret: secretMarker);
        using var malformedJsonResponse = await host.Client.SendAsync(malformedJson);

        using var malformedSignature = WebhookTestHost.SignedRequest(ValidBody());
        malformedSignature.Headers.Remove(
            SiteStateWebhookAuthenticator.SignatureHeaderName);
        malformedSignature.Headers.TryAddWithoutValidation(
            SiteStateWebhookAuthenticator.SignatureHeaderName,
            signatureMarker);
        using var malformedSignatureResponse =
            await host.Client.SendAsync(malformedSignature);

        var combined = string.Join(Environment.NewLine, logs.Messages);
        Assert.DoesNotContain(secretMarker, combined, StringComparison.Ordinal);
        Assert.DoesNotContain(rawBodyMarker, combined, StringComparison.Ordinal);
        Assert.DoesNotContain(signatureMarker, combined, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Suite", "YAGOT02Endpoint")]
    public void WebhookRouteMatcher_IsExact()
    {
        Assert.True(SiteStateWebhookRoute.Matches(SiteStateWebhookRoute.Path));
        Assert.True(SiteStateWebhookRoute.Matches(
            SiteStateWebhookRoute.Path.ToUpperInvariant()));
        Assert.False(SiteStateWebhookRoute.Matches(
            SiteStateWebhookRoute.Path + "/extra"));
        Assert.False(SiteStateWebhookRoute.Matches("/api/integration"));
    }

    [Theory]
    [InlineData("/api/integration/site-state/extra")]
    [InlineData("/Products")]
    [Trait("Suite", "YAGOT02Endpoint")]
    public async Task AdjacentAndNormalPaths_DoNotGainWebhookBypass(string path)
    {
        await using var host = await WebhookTestHost.CreateAsync(
            new CapturingApplyService());

        using var response = await host.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, host.LegacyStatusCallCount);
    }

    internal static byte[] ValidBody(
        long revision = 1,
        string mode = "Online",
        string siteName = "YAGOT") => Encoding.UTF8.GetBytes($$"""
        {"contractVersion":1,"siteId":1,"mode":"{{mode}}","revision":{{revision}},"effectiveAtUtc":"2026-09-04T10:00:00+00:00","expiresAtUtc":"2026-10-01T21:00:00+00:00","siteName":"{{siteName}}","siteUrl":"https://yagot.example","startDate":"2026-09-01","originalDurationDays":30}
        """);

    private static byte[] InvalidBody(string variation)
    {
        var json = Encoding.UTF8.GetString(ValidBody());
        json = variation switch
        {
            "wrong-version" => json.Replace(
                "\"contractVersion\":1", "\"contractVersion\":2"),
            "wrong-site" => json.Replace("\"siteId\":1", "\"siteId\":2"),
            "invalid-mode" => json.Replace("\"Online\"", "\"online\""),
            "missing-property" => json.Replace(
                ",\"originalDurationDays\":30", string.Empty),
            "duplicate-property" => json.Replace(
                "{\"contractVersion\":1",
                "{\"contractVersion\":1,\"contractVersion\":1"),
            "unknown-property" => json.Replace(
                "{\"contractVersion\":1",
                "{\"unknown\":true,\"contractVersion\":1"),
            "quoted-contract-version" => json.Replace(
                "\"contractVersion\":1", "\"contractVersion\":\"1\""),
            "quoted-site-id" => json.Replace(
                "\"siteId\":1", "\"siteId\":\"1\""),
            "quoted-revision" => json.Replace(
                "\"revision\":1", "\"revision\":\"1\""),
            "quoted-duration" => json.Replace(
                "\"originalDurationDays\":30",
                "\"originalDurationDays\":\"30\""),
            "numeric-mode" => json.Replace(
                "\"mode\":\"Online\"", "\"mode\":1"),
            "boolean-site-name" => json.Replace(
                "\"siteName\":\"YAGOT\"", "\"siteName\":true"),
            "numeric-effective-at" => json.Replace(
                "\"effectiveAtUtc\":\"2026-09-04T10:00:00+00:00\"",
                "\"effectiveAtUtc\":1788516000"),
            "numeric-start-date" => json.Replace(
                "\"startDate\":\"2026-09-01\"",
                "\"startDate\":20260901"),
            _ => throw new ArgumentOutOfRangeException(nameof(variation))
        };
        return Encoding.UTF8.GetBytes(json);
    }

    private static void ReplaceHeader(
        HttpRequestMessage request,
        string name,
        string value)
    {
        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }

    private sealed class CountingParser : ISiteStateSnapshotV1JsonParser
    {
        public int CallCount { get; private set; }

        public SiteStateSnapshotParseResult Parse(ReadOnlyMemory<byte> rawBody)
        {
            CallCount++;
            throw new InvalidOperationException("Parser must not run.");
        }
    }
}

internal sealed class WebhookTestHost : IAsyncDisposable
{
    private readonly WebApplication _application;
    private readonly CountingLegacyStatusHandler _legacyStatus;

    private WebhookTestHost(
        WebApplication application,
        CountingLegacyStatusHandler legacyStatus)
    {
        _application = application;
        _legacyStatus = legacyStatus;
        Client = application.GetTestClient();
        Client.BaseAddress = new Uri("https://localhost");
    }

    public HttpClient Client { get; }
    public IServiceProvider Services => _application.Services;
    public int LegacyStatusCallCount => _legacyStatus.CallCount;

    public static async Task<WebhookTestHost> CreateAsync(
        ISiteStateApplyService applyService,
        ISiteStateSnapshotV1JsonParser? parser = null,
        CapturingLogProvider? logs = null,
        string secret = SiteStateWebhookAuthenticationTests.TestSecret) =>
        await CreateCoreAsync(
            services => services.AddSingleton(applyService),
            parser,
            logs,
            secret);

    public static async Task<WebhookTestHost> CreateWithDatabaseAsync(
        PostgreSqlTestDatabase database) =>
        await CreateCoreAsync(services =>
        {
            services.AddDbContext<YAGOT_2._0.Models.NeondbContext>(options =>
                options.UseNpgsql(database.ConnectionString, npgsqlOptions =>
                    npgsqlOptions.EnableRetryOnFailure(
                        5,
                        TimeSpan.FromMilliseconds(200),
                        null)));
            services.AddLocalSiteRuntimeState();
        });

    private static async Task<WebhookTestHost> CreateCoreAsync(
        Action<IServiceCollection> configureApplyService,
        ISiteStateSnapshotV1JsonParser? parser = null,
        CapturingLogProvider? logs = null,
        string secret = SiteStateWebhookAuthenticationTests.TestSecret)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        if (logs is not null)
        {
            builder.Logging.AddProvider(logs);
        }

        builder.Services.AddCors(options => options.AddPolicy(
            "AllowAll",
            policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
        builder.Services.AddControllers().AddApplicationPart(
            typeof(SiteStateIntegrationController).Assembly);
        builder.Services.AddMemoryCache();
        var legacyStatus = new CountingLegacyStatusHandler();
        builder.Services.AddSingleton(legacyStatus);
        builder.Services.AddSingleton(_ => new HttpClient(legacyStatus)
        {
            BaseAddress = new Uri("https://legacy-control-panel.test/")
        });
        builder.Services.AddSingleton<DealingAPI>(services => new DealingAPI(
            services.GetRequiredService<HttpClient>(),
            builder.Configuration,
            services.GetRequiredService<ILogger<DealingAPI>>()));
        builder.Services.Configure<SiteStateWebhookOptions>(options =>
        {
            options.SiteId = 1;
            options.WebhookKeyId = SiteStateWebhookAuthenticationTests.TestKeyId;
            options.WebhookSecret = secret;
            options.TimestampToleranceSeconds = 300;
        });
        builder.Services.AddSingleton<TimeProvider>(new FixedWebhookTimeProvider());
        builder.Services.AddSingleton<ISiteStateWebhookAuthenticator,
            SiteStateWebhookAuthenticator>();
        builder.Services.AddSingleton<ISiteStateSnapshotV1JsonParser>(
            parser ?? new SiteStateSnapshotV1JsonParser());
        configureApplyService(builder.Services);

        var app = builder.Build();
        app.UseRouting();
        app.UseSiteStateWebhookProtocol();
        app.UseCors("AllowAll");
        app.UseLegacySiteStatusWithWebhookBypass();
        app.MapControllers();
        await app.StartAsync();
        return new WebhookTestHost(app, legacyStatus);
    }

    public async Task<HttpResponseMessage> SendSignedAsync(
        byte[] body,
        Guid? deliveryId = null)
    {
        using var request = SignedRequest(body, deliveryId: deliveryId);
        return await Client.SendAsync(request);
    }

    public static HttpRequestMessage SignedRequest(
        byte[] body,
        HttpContent? content = null,
        Guid? deliveryId = null,
        string secret = SiteStateWebhookAuthenticationTests.TestSecret)
    {
        var timestamp = SiteStateWebhookAuthenticationTests.NowUnixSeconds.ToString();
        var delivery = (deliveryId ??
                        SiteStateWebhookAuthenticationTests.DeliveryId).ToString("D");
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            SiteStateWebhookRoute.Path)
        {
            Content = content ?? new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType =
            MediaTypeHeaderValue.Parse("application/json; charset=utf-8");
        request.Headers.TryAddWithoutValidation(
            SiteStateWebhookAuthenticator.TimestampHeaderName,
            timestamp);
        request.Headers.TryAddWithoutValidation(
            SiteStateWebhookAuthenticator.DeliveryIdHeaderName,
            delivery);
        request.Headers.TryAddWithoutValidation(
            SiteStateWebhookAuthenticator.KeyIdHeaderName,
            SiteStateWebhookAuthenticationTests.TestKeyId);

        var prefix = Encoding.UTF8.GetBytes($"{timestamp}.{delivery}.");
        var input = new byte[prefix.Length + body.Length];
        prefix.CopyTo(input, 0);
        body.CopyTo(input, prefix.Length);
        var signature = "sha256=" + Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), input));
        request.Headers.TryAddWithoutValidation(
            SiteStateWebhookAuthenticator.SignatureHeaderName,
            signature);
        return request;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.DisposeAsync();
    }

    private sealed class FixedWebhookTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            DateTimeOffset.FromUnixTimeSeconds(
                SiteStateWebhookAuthenticationTests.NowUnixSeconds);
    }
}

internal sealed class CountingLegacyStatusHandler : HttpMessageHandler
{
    private int _callCount;

    public int CallCount => Volatile.Read(ref _callCount);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        });
    }
}

internal sealed class CapturingApplyService : ISiteStateApplyService
{
    public int CallCount { get; private set; }
    public SiteStateSnapshotV1? Snapshot { get; private set; }
    public SiteStateDeliveryContext? Delivery { get; private set; }
    public SiteStateApplyResult Result { get; init; } =
        new(SiteStateApplyOutcome.Applied);
    public Exception? Exception { get; init; }

    public Task<SiteStateApplyResult> ApplyAsync(
        SiteStateSnapshotV1 snapshot,
        SiteStateDeliveryContext? delivery = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        Snapshot = snapshot;
        Delivery = delivery;
        return Exception is null
            ? Task.FromResult(Result)
            : Task.FromException<SiteStateApplyResult>(Exception);
    }
}

internal sealed class CancellationAwareApplyService : ISiteStateApplyService
{
    public TaskCompletionSource Entered { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool ObservedCancellation { get; private set; }

    public async Task<SiteStateApplyResult> ApplyAsync(
        SiteStateSnapshotV1 snapshot,
        SiteStateDeliveryContext? delivery = null,
        CancellationToken cancellationToken = default)
    {
        Entered.TrySetResult();
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation wait completed.");
        }
        catch (OperationCanceledException)
        {
            ObservedCancellation = true;
            throw;
        }
    }
}

internal sealed class StreamingContent : HttpContent
{
    private readonly byte[] _body;

    public StreamingContent(byte[] body)
    {
        _body = body;
    }

    protected override Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context) =>
        stream.WriteAsync(_body).AsTask();

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}

internal sealed class CapturingLogProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public ILogger CreateLogger(string categoryName) =>
        new CapturingLogger(Messages);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _messages;

        public CapturingLogger(ConcurrentQueue<string> messages)
        {
            _messages = messages;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _messages.Enqueue(formatter(state, exception));
    }
}
