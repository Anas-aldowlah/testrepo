using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services.Integration;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

internal sealed class Yagot07EndToEndHost : IAsyncDisposable
{
    internal const string SnapshotApiKey = "yagot07-snapshot-api-key-distinct";
    private readonly WebApplication _application;
    private readonly Yagot07ActionRecorder _recorder;

    private Yagot07EndToEndHost(
        WebApplication application,
        Yagot07ActionRecorder recorder,
        Yagot07ControlPanelTransport controlPanel,
        Yagot07SnapshotQueryInterceptor queries,
        Yagot07FailingSaveInterceptor failingSave,
        Yagot07DiagnosticsCheckpointFailureInterceptor failingDiagnostics,
        Yagot07BackgroundReconciliationPolicy backgroundPolicy,
        Yagot07ReconciliationRunObserver reconciliationRuns,
        TimeProvider clock)
    {
        _application = application;
        _recorder = recorder;
        ControlPanel = controlPanel;
        SnapshotQueries = queries;
        FailingSave = failingSave;
        FailingDiagnostics = failingDiagnostics;
        BackgroundPolicy = backgroundPolicy;
        ReconciliationRuns = reconciliationRuns;
        Clock = clock;
        Client = application.GetTestClient();
        Client.BaseAddress = new Uri("https://localhost");
    }

    public HttpClient Client { get; }
    public IServiceProvider Services => _application.Services;
    public Yagot07ControlPanelTransport ControlPanel { get; }
    public Yagot07SnapshotQueryInterceptor SnapshotQueries { get; }
    public Yagot07FailingSaveInterceptor FailingSave { get; }
    public Yagot07DiagnosticsCheckpointFailureInterceptor FailingDiagnostics { get; }
    public Yagot07BackgroundReconciliationPolicy BackgroundPolicy { get; }
    public Yagot07ReconciliationRunObserver ReconciliationRuns { get; }
    public TimeProvider Clock { get; }
    public int ActionCount => _recorder.Count;

    public static async Task<Yagot07EndToEndHost> CreateAsync(
        PostgreSqlTestDatabase database,
        TimeProvider? clock = null,
        params IInterceptor[] extraInterceptors)
        => await CreateAsync(
            database,
            new Yagot07HostOptions(clock),
            extraInterceptors);

    public static async Task<Yagot07EndToEndHost> CreateAsync(
        PostgreSqlTestDatabase database,
        Yagot07HostOptions options,
        params IInterceptor[] extraInterceptors)
    {
        var repositoryRoot = FindRepositoryRoot();
        var effectiveClock = options.Clock ?? new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
            DateTimeOffset.FromUnixTimeSeconds(SiteStateWebhookAuthenticationTests.NowUnixSeconds));
        var controlPanel = new Yagot07ControlPanelTransport();
        var queryCounter = new Yagot07SnapshotQueryInterceptor();
        var failingSave = new Yagot07FailingSaveInterceptor();
        var failingDiagnostics = new Yagot07DiagnosticsCheckpointFailureInterceptor();
        var allInterceptors = new IInterceptor[] { queryCounter, failingSave, failingDiagnostics }
            .Concat(extraInterceptors)
            .ToArray();
        var recorder = new Yagot07ActionRecorder();
        var backgroundPolicy = new Yagot07BackgroundReconciliationPolicy(
            options.BackgroundStartupDelay ?? TimeSpan.FromHours(1));
        var reconciliationRuns = new Yagot07ReconciliationRunObserver();

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
            [$"{SiteStateWebhookOptions.SectionName}:TimestampToleranceSeconds"] = "300",
            [$"{SiteStateReconciliationOptions.SectionName}:ControlPanelBaseUrl"] =
                "https://control-panel.test/",
            [$"{SiteStateReconciliationOptions.SectionName}:SnapshotApiKey"] = SnapshotApiKey,
            [$"{SiteStateReconciliationOptions.SectionName}:ReconciliationIntervalMinutes"] = "30",
            [$"{SiteStateReconciliationOptions.SectionName}:SnapshotHttpTimeoutSeconds"] = "10"
        });
        builder.Logging.ClearProviders();
        builder.Services.AddControllersWithViews()
            .AddApplicationPart(typeof(Yagot.Controllers.HomeController).Assembly)
            .AddApplicationPart(typeof(Yagot07StorefrontProbeController).Assembly);
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
        builder.Services.AddSingleton(effectiveClock);
        builder.Services.AddSingleton<TimeProvider>(effectiveClock);
        builder.Services.AddSingleton(recorder);
        builder.Services.AddSingleton(controlPanel);
        builder.Services.AddSingleton(backgroundPolicy);
        builder.Services.AddSingleton(reconciliationRuns);
        builder.Services.AddDbContext<NeondbContext>(options =>
        {
            options.UseNpgsql(database.ConnectionString, npgsql =>
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromMilliseconds(200), null));
            options.AddInterceptors(allInterceptors);
        });
        builder.Services.AddLocalSiteRuntimeState();
        if (options.ReadBarrier is not null)
        {
            builder.Services.RemoveAll<ILocalSiteStateReader>();
            builder.Services.AddScoped<LocalSiteStateReader>();
            builder.Services.AddScoped<ILocalSiteStateReader>(provider =>
                new Yagot07HeldLocalSiteStateReader(
                    provider.GetRequiredService<LocalSiteStateReader>(),
                    options.ReadBarrier));
        }
        builder.Services.AddScoped<ISiteAccessDecisionService, SiteAccessDecisionService>();
        builder.Services.AddScoped<SiteStatusFilter>();
        builder.Services.AddScoped<SiteStatusFilterAdmin>();
        builder.Services.AddSiteStateWebhook(builder.Configuration);
        builder.Services.AddSiteStateReconciliation(builder.Configuration);
        builder.Services.AddHttpClient<IControlPanelSnapshotClient, ControlPanelSnapshotClient>()
            .ConfigurePrimaryHttpMessageHandler(() => controlPanel);
        builder.Services.RemoveAll<ISiteStateReconciliationPolicy>();
        builder.Services.AddSingleton<ISiteStateReconciliationPolicy>(backgroundPolicy);
        builder.Services.RemoveAll<ISiteStateReconciliationCoordinator>();
        builder.Services.AddSingleton<SiteStateReconciliationCoordinator>();
        builder.Services.AddSingleton<ISiteStateReconciliationCoordinator>(provider =>
            new Yagot07ObservingReconciliationCoordinator(
                provider.GetRequiredService<SiteStateReconciliationCoordinator>(),
                reconciliationRuns));

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
        app.MapControllerRoute("areas", "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        return new Yagot07EndToEndHost(
            app,
            recorder,
            controlPanel,
            queryCounter,
            failingSave,
            failingDiagnostics,
            backgroundPolicy,
            reconciliationRuns,
            effectiveClock);
    }

    public async Task<SiteStateApplyResult> ApplyAsync(SiteStateSnapshotV1 snapshot)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISiteStateApplyService>()
            .ApplyAsync(snapshot);
    }

    public async Task<SiteStateReconciliationResult> ReconcileAsync(
        SiteStateReconciliationReason reason = SiteStateReconciliationReason.OperatorRecovery)
    {
        var trigger = await Services.GetRequiredService<ISiteStateReconciliationCoordinator>()
            .RequestAsync(reason, CancellationToken.None);
        return trigger.Result ?? throw new InvalidOperationException("Reconciliation was already running.");
    }

    public async Task<HttpResponseMessage> RequestAsync(
        SiteAccessSurface surface,
        string? role = null,
        bool json = false)
    {
        var path = surface == SiteAccessSurface.Admin
            ? "/Admin/_yagot07"
            : "/_yagot07/storefront";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (role is not null)
        {
            request.Headers.TryAddWithoutValidation(Yagot05TestAuthenticationHandler.RoleHeader, role);
        }
        if (json)
        {
            request.Headers.Accept.ParseAdd("application/json");
        }
        return await Client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> SendWebhookAsync(
        SiteStateSnapshotV1 snapshot,
        Guid? deliveryId = null,
        string? secret = null)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return await Client.SendAsync(SignedWebhookRequest(
            body,
            deliveryId ?? Guid.NewGuid(),
            secret ?? SiteStateWebhookAuthenticationTests.TestSecret));
    }

    public HttpRequestMessage SignedWebhookRequest(
        byte[] body,
        Guid deliveryId,
        string secret)
    {
        var timestamp = Clock.GetUtcNow().ToUnixTimeSeconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var delivery = deliveryId.ToString("D");
        var prefix = Encoding.UTF8.GetBytes($"{timestamp}.{delivery}.");
        var input = new byte[prefix.Length + body.Length];
        prefix.CopyTo(input, 0);
        body.CopyTo(input, prefix.Length);
        var signature = "sha256=" + Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), input));
        var request = new HttpRequestMessage(HttpMethod.Post, SiteStateWebhookRoute.Path)
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new("application/json") { CharSet = "utf-8" };
        request.Headers.TryAddWithoutValidation(SiteStateWebhookAuthenticator.TimestampHeaderName, timestamp);
        request.Headers.TryAddWithoutValidation(SiteStateWebhookAuthenticator.DeliveryIdHeaderName, delivery);
        request.Headers.TryAddWithoutValidation(SiteStateWebhookAuthenticator.KeyIdHeaderName,
            SiteStateWebhookAuthenticationTests.TestKeyId);
        request.Headers.TryAddWithoutValidation(SiteStateWebhookAuthenticator.SignatureHeaderName, signature);
        return request;
    }

    public async Task<(LocalSiteStateSnapshot? Snapshot, int ReceiptCount, SiteStateSyncCheckpoint? Checkpoint)>
        ReadDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NeondbContext>();
        return (
            await context.LocalSiteStateSnapshots.AsNoTracking().SingleOrDefaultAsync(),
            await context.SiteStateEventReceipts.CountAsync(),
            await context.SiteStateSyncCheckpoints.AsNoTracking().SingleOrDefaultAsync());
    }

    public void ResetActions() => _recorder.Reset();

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.DisposeAsync();
    }

    internal static SiteStateSnapshotV1 Snapshot(
        long revision,
        string mode,
        DateTimeOffset? expiresAtUtc = null,
        string siteName = "YAGOT") =>
        SiteStateContractTests.ValidSnapshot(revision: revision, mode: mode, siteName: siteName) with
        {
            ExpiresAtUtc = expiresAtUtc ?? new DateTimeOffset(2026, 10, 7, 21, 0, 0, TimeSpan.Zero)
        };

    private static async Task AdminGateAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }
        if (!(context.User.Identity?.IsAuthenticated ?? false))
        {
            await WriteGateAsync(context, StatusCodes.Status401Unauthorized, "session_expired");
            return;
        }
        if (!context.User.IsInRole("Admin") && !context.User.IsInRole("Developer"))
        {
            await WriteGateAsync(context, StatusCodes.Status403Forbidden, "forbidden");
            return;
        }
        await next(context);
    }

    private static Task WriteGateAsync(HttpContext context, int statusCode, string code)
    {
        context.Response.StatusCode = statusCode;
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(new
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
