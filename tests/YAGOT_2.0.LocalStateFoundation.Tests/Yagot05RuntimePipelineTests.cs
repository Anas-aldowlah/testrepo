using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class Yagot05RuntimePipelineTests
{
    [Theory]
    [InlineData(Yagot05RuntimeState.Development, null, false, HttpStatusCode.Redirect, null)]
    [InlineData(Yagot05RuntimeState.Development, "Customer", true, HttpStatusCode.ServiceUnavailable, "site_development")]
    [InlineData(Yagot05RuntimeState.Offline, null, false, HttpStatusCode.Redirect, null)]
    [InlineData(Yagot05RuntimeState.Offline, "Customer", true, HttpStatusCode.ServiceUnavailable, "site_offline")]
    [InlineData(Yagot05RuntimeState.Missing, null, false, HttpStatusCode.ServiceUnavailable, null)]
    [InlineData(Yagot05RuntimeState.Missing, "Customer", true, HttpStatusCode.ServiceUnavailable, "site_state_missing")]
    [InlineData(Yagot05RuntimeState.StorageFailure, null, false, HttpStatusCode.ServiceUnavailable, null)]
    [InlineData(Yagot05RuntimeState.StorageFailure, "Customer", true, HttpStatusCode.ServiceUnavailable, "site_state_storage_unavailable")]
    public async Task DeniedStorefrontRequests_StopActionAndNeverCallControlPanel(
        Yagot05RuntimeState state,
        string? role,
        bool json,
        HttpStatusCode expectedStatus,
        string? expectedCode)
    {
        await using var host = await Yagot05RuntimeHost.CreateAsync(state);
        using var request = Request(HttpMethod.Get, "/_yagot05/storefront", role, json);

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(0, host.ActionCount);
        Assert.Equal(1, host.ProviderReadCount);
        Assert.Equal(0, host.OutboundCallCount);
        if (json)
        {
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Contains(
                $"\"code\":\"{expectedCode}\"",
                await response.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
            Assert.Null(response.Headers.Location);
        }
    }

    [Theory]
    [InlineData(Yagot05RuntimeState.Online, null, "/_yagot05/storefront")]
    [InlineData(Yagot05RuntimeState.Online, "Customer", "/_yagot05/storefront")]
    [InlineData(Yagot05RuntimeState.Online, "Admin", "/Admin/_yagot05")]
    [InlineData(Yagot05RuntimeState.Development, "Admin", "/Admin/_yagot05")]
    [InlineData(Yagot05RuntimeState.Offline, "Developer", "/_yagot05/storefront")]
    public async Task AllowedRequestsExecuteOnlyFromLocalState(
        Yagot05RuntimeState state,
        string? role,
        string path)
    {
        await using var host = await Yagot05RuntimeHost.CreateAsync(state);
        using var request = Request(HttpMethod.Get, path, role, json: false);

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, host.ActionCount);
        Assert.Equal(1, host.ProviderReadCount);
        Assert.Equal(0, host.OutboundCallCount);
    }

    [Theory]
    [InlineData(Yagot05RuntimeState.Offline, "site_offline")]
    [InlineData(Yagot05RuntimeState.StorageFailure, "site_state_storage_unavailable")]
    public async Task ValidAdminIsDeniedByLocalStateOnlyAfterRoleGate(
        Yagot05RuntimeState state,
        string expectedCode)
    {
        await using var host = await Yagot05RuntimeHost.CreateAsync(state);
        using var request = Request(
            HttpMethod.Get,
            "/Admin/_yagot05",
            "Admin",
            json: true);

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains(
            $"\"code\":\"{expectedCode}\"",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        Assert.Equal(0, host.ActionCount);
        Assert.Equal(1, host.ProviderReadCount);
        Assert.Equal(0, host.OutboundCallCount);
    }

    [Fact]
    public async Task DeveloperBypassesProviderFailureWithoutControlPanelFallback()
    {
        await using var host = await Yagot05RuntimeHost.CreateAsync(
            Yagot05RuntimeState.StorageFailure);
        using var request = Request(
            HttpMethod.Get,
            "/_yagot05/storefront",
            "Developer",
            json: false);

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, host.ActionCount);
        Assert.Equal(1, host.ProviderReadCount);
        Assert.Equal(0, host.OutboundCallCount);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized, "session_expired", 0, 0)]
    [InlineData("Customer", HttpStatusCode.Forbidden, "forbidden", 0, 0)]
    [InlineData("Admin", HttpStatusCode.OK, null, 1, 1)]
    public async Task AdminAuthenticationAndRoleGateRunBeforeSiteEnforcement(
        string? role,
        HttpStatusCode expectedStatus,
        string? expectedCode,
        int expectedProviderReads,
        int expectedActions)
    {
        await using var host = await Yagot05RuntimeHost.CreateAsync(Yagot05RuntimeState.Online);
        using var request = Request(HttpMethod.Get, "/Admin/_yagot05", role, json: true);

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(expectedProviderReads, host.ProviderReadCount);
        Assert.Equal(expectedActions, host.ActionCount);
        Assert.Equal(0, host.OutboundCallCount);
        if (expectedCode is not null)
        {
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Contains(
                $"\"code\":\"{expectedCode}\"",
                await response.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(Yagot05RuntimeState.Missing)]
    [InlineData(Yagot05RuntimeState.Online)]
    [InlineData(Yagot05RuntimeState.Development)]
    [InlineData(Yagot05RuntimeState.Offline)]
    [InlineData(Yagot05RuntimeState.StorageFailure)]
    public async Task WebhookPostBypassesLocalEnforcementAndKeepsProtocol(
        Yagot05RuntimeState state)
    {
        await using var host = await Yagot05RuntimeHost.CreateAsync(state);
        using var request = WebhookTestHost.SignedRequest(
            SiteStateWebhookEndpointTests.ValidBody());

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, host.ApplyCallCount);
        Assert.Equal(0, host.ProviderReadCount);
        Assert.Equal(0, host.OutboundCallCount);
    }

    [Fact]
    public async Task WebhookNonPostKeeps405WithoutEnforcement()
    {
        await using var host = await Yagot05RuntimeHost.CreateAsync(
            Yagot05RuntimeState.StorageFailure);

        using var response = await host.Client.GetAsync(SiteStateWebhookRoute.Path);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("POST", response.Content.Headers.Allow.Single());
        Assert.Equal(0, host.ProviderReadCount);
        Assert.Equal(0, host.OutboundCallCount);
    }

    [Fact]
    public async Task MaintenanceUnavailableAndStaticRoutesRemainReachableWithoutLoops()
    {
        await using var host = await Yagot05RuntimeHost.CreateAsync(
            Yagot05RuntimeState.StorageFailure);

        using var developer = await host.Client.GetAsync("/DirectiveDevClose/Developer");
        Assert.Equal(HttpStatusCode.OK, developer.StatusCode);
        Assert.Equal(0, host.ProviderReadCount);

        using var asset = await host.Client.GetAsync("/css/yaqut-theme.css");
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Equal(0, host.ProviderReadCount);

        host.SetState(Yagot05RuntimeState.Offline);
        using var close = await host.Client.GetAsync(
            "/DirectiveDevClose/close?SiteName=untrusted&Url=javascript%3Aalert(1)");
        var closeBody = await close.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.Contains("YAGOT", closeBody, StringComparison.Ordinal);
        Assert.DoesNotContain("untrusted", closeBody, StringComparison.Ordinal);
        Assert.Equal(1, host.ProviderReadCount);

        host.SetState(Yagot05RuntimeState.Online);
        using var reopened = await host.Client.GetAsync("/DirectiveDevClose/close");
        Assert.Equal(HttpStatusCode.Redirect, reopened.StatusCode);
        Assert.NotEqual(
            "/DirectiveDevClose/close",
            reopened.Headers.Location?.OriginalString);
        Assert.Equal(1, host.ProviderReadCount);

        host.SetState(Yagot05RuntimeState.Development);
        using var development = await host.Client.GetAsync("/DirectiveDevClose/close");
        Assert.Equal(HttpStatusCode.Redirect, development.StatusCode);
        Assert.Contains(
            "/DirectiveDevClose/Developer",
            development.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, host.ProviderReadCount);

        host.SetState(Yagot05RuntimeState.Missing);
        using var unavailable = await host.Client.GetAsync("/DirectiveDevClose/close");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal("no-store", unavailable.Headers.CacheControl?.ToString());
        Assert.Contains(
            "الخدمة غير متاحة مؤقتاً",
            await unavailable.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        Assert.Equal(1, host.ProviderReadCount);
        Assert.Equal(0, host.OutboundCallCount);
    }

    [Fact]
    public async Task OutboundObserver_PositiveControlDetectsAttemptedControlPanelRequest()
    {
        await using var host = await Yagot05RuntimeHost.CreateAsync(
            Yagot05RuntimeState.Online);

        await Assert.ThrowsAsync<HttpRequestException>(
            host.SendObservedControlPanelRequestAsync);

        Assert.Equal(1, host.OutboundCallCount);
    }

    private static HttpRequestMessage Request(
        HttpMethod method,
        string path,
        string? role,
        bool json)
    {
        var request = new HttpRequestMessage(method, path);
        if (role is not null)
        {
            request.Headers.TryAddWithoutValidation(
                Yagot05TestAuthenticationHandler.RoleHeader,
                role);
        }

        if (json)
        {
            request.Headers.Accept.ParseAdd("application/json");
        }

        return request;
    }
}

public enum Yagot05RuntimeState
{
    Missing,
    Online,
    Development,
    Offline,
    StorageFailure
}

[Route("_yagot05/storefront")]
[ServiceFilter(typeof(SiteStatusFilter))]
public sealed class Yagot05StorefrontProbeController(
    Yagot05ActionRecorder recorder) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        recorder.Record();
        return Ok(new { success = true });
    }
}

[Area("Admin")]
[Route("Admin/_yagot05")]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public sealed class Yagot05AdminProbeController(
    Yagot05ActionRecorder recorder) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        recorder.Record();
        return Ok(new { success = true });
    }
}

internal sealed class Yagot05RuntimeHost : IAsyncDisposable
{
    private readonly WebApplication _application;
    private readonly Yagot05RuntimeProvider _provider;
    private readonly Yagot05ActionRecorder _recorder;
    private readonly TestOutboundHttpObserver _outbound;
    private readonly CapturingApplyService _apply;

    private Yagot05RuntimeHost(
        WebApplication application,
        Yagot05RuntimeProvider provider,
        Yagot05ActionRecorder recorder,
        TestOutboundHttpObserver outbound,
        CapturingApplyService apply)
    {
        _application = application;
        _provider = provider;
        _recorder = recorder;
        _outbound = outbound;
        _apply = apply;
        Client = application.GetTestClient();
        Client.BaseAddress = new Uri("https://localhost");
    }

    public HttpClient Client { get; }
    public int ProviderReadCount => _provider.ReadCount;
    public int ActionCount => _recorder.Count;
    public int OutboundCallCount => _outbound.CallCount;
    public int ApplyCallCount => _apply.CallCount;

    public async Task SendObservedControlPanelRequestAsync()
    {
        var factory = _application.Services.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient();
        await client.GetAsync("https://control-panel.test/api/v1/sites/1/snapshot");
    }

    public void SetState(Yagot05RuntimeState state)
    {
        _provider.SetState(state);
        _recorder.Reset();
    }

    public static async Task<Yagot05RuntimeHost> CreateAsync(
        Yagot05RuntimeState state)
    {
        var repositoryRoot = FindRepositoryRoot();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Yagot.Controllers.HomeController).Assembly.FullName,
            ContentRootPath = repositoryRoot,
            WebRootPath = Path.Combine(repositoryRoot, "wwwroot"),
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{SiteStateWebhookOptions.SectionName}:SiteId"] = "1",
            [$"{SiteStateWebhookOptions.SectionName}:WebhookKeyId"] =
                SiteStateWebhookAuthenticationTests.TestKeyId,
            [$"{SiteStateWebhookOptions.SectionName}:WebhookSecret"] =
                SiteStateWebhookAuthenticationTests.TestSecret,
            [$"{SiteStateWebhookOptions.SectionName}:TimestampToleranceSeconds"] = "300"
        });
        builder.Logging.ClearProviders();

        builder.Services.AddControllersWithViews()
            .AddApplicationPart(typeof(Yagot.Controllers.HomeController).Assembly)
            .AddApplicationPart(typeof(Yagot05StorefrontProbeController).Assembly);
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession();
        builder.Services.AddCors(options => options.AddPolicy(
            "AllowAll",
            policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
        builder.Services.AddRateLimiter(_ => { });
        builder.Services.AddAuthentication(Yagot05TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, Yagot05TestAuthenticationHandler>(
                Yagot05TestAuthenticationHandler.SchemeName,
                _ => { });
        builder.Services.AddAuthorization();

        var provider = new Yagot05RuntimeProvider(state);
        var recorder = new Yagot05ActionRecorder();
        var outbound = new TestOutboundHttpObserver();
        var apply = new CapturingApplyService();
        builder.Services.AddSingleton<ILocalSiteRuntimeStateProvider>(provider);
        builder.Services.AddSingleton<TimeProvider>(new Yagot05FixedTimeProvider());
        builder.Services.AddScoped<ISiteAccessDecisionService, SiteAccessDecisionService>();
        builder.Services.AddScoped<SiteStatusFilter>();
        builder.Services.AddScoped<SiteStatusFilterAdmin>();
        builder.Services.AddSingleton(recorder);
        builder.Services.AddSingleton(outbound);
        builder.Services.AddSingleton<IHttpMessageHandlerBuilderFilter,
            TestOutboundHttpMessageHandlerBuilderFilter>();
        builder.Services.AddHttpClient();
        builder.Services.AddSiteStateWebhook(builder.Configuration);
        builder.Services.AddSingleton<ISiteStateApplyService>(apply);

        var app = builder.Build();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseSiteStateWebhookProtocol();
        app.UseRateLimiter();
        app.UseCors("AllowAll");
        app.UseSession();
        app.UseAuthentication();
        app.Use(AdminGateAsync);
        app.UseAuthorization();
        app.MapControllers();
        app.MapControllerRoute(
            "areas",
            "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
        app.MapControllerRoute(
            "default",
            "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();

        return new Yagot05RuntimeHost(app, provider, recorder, outbound, apply);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.DisposeAsync();
    }

    private static async Task AdminGateAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments(
                "/Admin",
                StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (!(context.User.Identity?.IsAuthenticated ?? false))
        {
            await WriteGateJsonAsync(context, StatusCodes.Status401Unauthorized, "session_expired");
            return;
        }

        if (!context.User.IsInRole("Admin") && !context.User.IsInRole("Developer"))
        {
            await WriteGateJsonAsync(context, StatusCodes.Status403Forbidden, "forbidden");
            return;
        }

        await next(context);
    }

    private static async Task WriteGateJsonAsync(
        HttpContext context,
        int statusCode,
        string code)
    {
        context.Response.StatusCode = statusCode;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            code,
            message = "Authentication gate response."
        });
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "YAGOT_2.0.csproj")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}

internal sealed class Yagot05RuntimeProvider : ILocalSiteRuntimeStateProvider
{
    private Yagot05RuntimeState _state;
    private int _readCount;

    public Yagot05RuntimeProvider(Yagot05RuntimeState state) => _state = state;

    public int ReadCount => Volatile.Read(ref _readCount);

    public void SetState(Yagot05RuntimeState state)
    {
        _state = state;
        Interlocked.Exchange(ref _readCount, 0);
    }

    public Task<LocalSiteStateReadResult> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _readCount);
        return _state switch
        {
            Yagot05RuntimeState.Missing =>
                Task.FromResult(LocalSiteStateReadResult.Missing),
            Yagot05RuntimeState.Online => Found(SiteStateContractV1.Online),
            Yagot05RuntimeState.Development => Found(SiteStateContractV1.Development),
            Yagot05RuntimeState.Offline => Found(SiteStateContractV1.Offline),
            Yagot05RuntimeState.StorageFailure =>
                Task.FromException<LocalSiteStateReadResult>(
                    new LocalSiteRuntimeStateReadException(
                        LocalSiteRuntimeStateReadFailure.StorageUnavailable)),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static Task<LocalSiteStateReadResult> Found(string mode) =>
        Task.FromResult(LocalSiteStateReadResult.Found(
            SiteStateContractTests.ValidSnapshot(mode: mode)));
}

public sealed class Yagot05ActionRecorder
{
    private int _count;
    public int Count => Volatile.Read(ref _count);
    public void Record() => Interlocked.Increment(ref _count);
    public void Reset() => Interlocked.Exchange(ref _count, 0);
}

internal sealed class TestOutboundHttpObserver
{
    private int _callCount;
    public int CallCount => Volatile.Read(ref _callCount);

    public void Record() => Interlocked.Increment(ref _callCount);
}

internal sealed class TestOutboundHttpMessageHandlerBuilderFilter(
    TestOutboundHttpObserver observer) : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(
        Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder);
        builder.AdditionalHandlers.Insert(0, new BlockingOutboundHandler(observer));
    };

    private sealed class BlockingOutboundHandler(
        TestOutboundHttpObserver observer) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            observer.Record();
            throw new HttpRequestException(
                $"Observed unexpected outbound HTTP request to {request.RequestUri?.Host}.");
        }
    }
}

internal sealed class Yagot05FixedTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() =>
        DateTimeOffset.FromUnixTimeSeconds(
            SiteStateWebhookAuthenticationTests.NowUnixSeconds);
}

internal sealed class Yagot05TestAuthenticationHandler :
    AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Yagot05Test";
    public const string RoleHeader = "X-Yagot05-Test-Role";

    public Yagot05TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers[RoleHeader].SingleOrDefault();
        if (string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, $"test-{role}"),
                new Claim(ClaimTypes.Role, role)
            },
            SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, SchemeName)));
    }
}
