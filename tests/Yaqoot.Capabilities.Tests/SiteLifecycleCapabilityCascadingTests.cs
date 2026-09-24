using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class SiteLifecycleCapabilityCascadingTests
{
    private readonly CapabilityCatalog _catalog = new();

    private sealed class FakeSiteRuntimeStateProvider(SiteStateSnapshotV1? snapshot) : ILocalSiteRuntimeStateProvider
    {
        public SiteStateSnapshotV1? CurrentSnapshot { get; set; } = snapshot;

        public Task<LocalSiteStateReadResult> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CurrentSnapshot is not null
                ? LocalSiteStateReadResult.Found(CurrentSnapshot)
                : LocalSiteStateReadResult.Missing);
    }

    private LocalCapabilityRuntimeStateProvider CreateAllEnabledRuntime()
    {
        var runtime = new LocalCapabilityRuntimeStateProvider(_catalog, TimeProvider.System);
        var modules = _catalog.Modules.Select(m => new CapabilityModuleStateV1(m.Code, true)).ToArray();
        var features = _catalog.Features.Select(f => new CapabilityFeatureStateV1(f.Code, f.ModuleCode, true)).ToArray();
        var snapshot = new CapabilitySnapshotV1(1, "yaqoot-capabilities-1", 1, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, modules, features);
        runtime.Publish(snapshot);
        return runtime;
    }

    private static SiteStateSnapshotV1 CreateSiteSnapshot(string mode, DateTimeOffset expiresAtUtc) =>
        new(1, 1, mode, 1, DateTimeOffset.UtcNow, expiresAtUtc, "Test Store", "https://test.com", DateOnly.FromDateTime(DateTime.UtcNow), 30);

    [Fact]
    public void When_SiteIsOffline_CoreRemainsEnabled_BusinessModulesAreOperationallyUnavailable()
    {
        var runtime = CreateAllEnabledRuntime();
        var siteState = new FakeSiteRuntimeStateProvider(CreateSiteSnapshot("Offline", DateTimeOffset.UtcNow.AddDays(10)));
        var evaluator = new CapabilityEvaluator(_catalog, runtime, siteState, TimeProvider.System);

        var coreResult = evaluator.EvaluateModule(CapabilityModuleCodes.Core);
        Assert.True(coreResult.IsEnabled);
        Assert.Equal(CapabilityEvaluationReason.CoreRequired, coreResult.Reason);

        var businessModules = _catalog.Modules.Where(m => !m.IsCore).ToArray();
        Assert.Equal(7, businessModules.Length);
        Assert.All(businessModules, m =>
        {
            var result = evaluator.EvaluateModule(m.Code);
            Assert.False(result.IsEnabled, $"Module {m.Code} should be disabled when site is Offline.");
            Assert.Equal(CapabilityEvaluationReason.OperationallyUnavailable, result.Reason);
        });
    }

    [Fact]
    public void When_SiteIsOffline_BusinessFeaturesAreOperationallyUnavailable()
    {
        var runtime = CreateAllEnabledRuntime();
        var siteState = new FakeSiteRuntimeStateProvider(CreateSiteSnapshot("Offline", DateTimeOffset.UtcNow.AddDays(10)));
        var evaluator = new CapabilityEvaluator(_catalog, runtime, siteState, TimeProvider.System);

        var coreFeatures = _catalog.Features.Where(f => f.IsCore).ToArray();
        Assert.All(coreFeatures, f =>
        {
            var result = evaluator.EvaluateFeature(f.Code);
            Assert.True(result.IsEnabled);
            Assert.Equal(CapabilityEvaluationReason.CoreRequired, result.Reason);
        });

        var businessFeatures = _catalog.Features.Where(f => !f.IsCore).ToArray();
        Assert.Equal(43, businessFeatures.Length);
        Assert.All(businessFeatures, f =>
        {
            var result = evaluator.EvaluateFeature(f.Code);
            Assert.False(result.IsEnabled, $"Feature {f.Code} should be disabled when site is Offline.");
            Assert.Equal(CapabilityEvaluationReason.OperationallyUnavailable, result.Reason);
        });
    }

    [Fact]
    public void When_SiteIsExpired_BusinessModulesAndFeaturesAreOperationallyUnavailable()
    {
        var runtime = CreateAllEnabledRuntime();
        // Expired yesterday
        var siteState = new FakeSiteRuntimeStateProvider(CreateSiteSnapshot("Online", DateTimeOffset.UtcNow.AddDays(-1)));
        var evaluator = new CapabilityEvaluator(_catalog, runtime, siteState, TimeProvider.System);

        var coreResult = evaluator.EvaluateModule(CapabilityModuleCodes.Core);
        Assert.True(coreResult.IsEnabled);

        var productsResult = evaluator.EvaluateModule(CapabilityModuleCodes.ProductsInventory);
        Assert.False(productsResult.IsEnabled);
        Assert.Equal(CapabilityEvaluationReason.OperationallyUnavailable, productsResult.Reason);

        var productViewResult = evaluator.EvaluateFeature(CapabilityFeatureCodes.ProductView);
        Assert.False(productViewResult.IsEnabled);
        Assert.Equal(CapabilityEvaluationReason.OperationallyUnavailable, productViewResult.Reason);
    }

    [Fact]
    public void When_SiteIsOnlineAndNotExpired_CapabilitiesEvaluateNormally()
    {
        var runtime = CreateAllEnabledRuntime();
        var siteState = new FakeSiteRuntimeStateProvider(CreateSiteSnapshot("Online", DateTimeOffset.UtcNow.AddDays(30)));
        var evaluator = new CapabilityEvaluator(_catalog, runtime, siteState, TimeProvider.System);

        Assert.True(evaluator.IsModuleEnabled(CapabilityModuleCodes.ProductsInventory));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.ProductView));
    }

    [Fact]
    public void When_SiteRuntimeStateIsNull_BackwardCompatibilityIsPreserved()
    {
        var runtime = CreateAllEnabledRuntime();
        var evaluator = new CapabilityEvaluator(_catalog, runtime);

        Assert.True(evaluator.IsModuleEnabled(CapabilityModuleCodes.ProductsInventory));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.ProductView));
    }

    [Fact]
    public async Task When_SiteIsOffline_RequireCapabilityFilter_BlocksHtmlWithRedirect()
    {
        var runtime = CreateAllEnabledRuntime();
        var siteState = new FakeSiteRuntimeStateProvider(CreateSiteSnapshot("Offline", DateTimeOffset.UtcNow.AddDays(10)));
        var evaluator = new CapabilityEvaluator(_catalog, runtime, siteState, TimeProvider.System);

        var filter = new RequireCapabilityFilter(evaluator, NullLogger<RequireCapabilityFilter>.Instance, CapabilityFeatureCodes.ProductView);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Accept"] = "text/html";
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());

        var nextCalled = false;
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        Assert.False(nextCalled);
        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/Home/NotFoundPage?statusCode=403", redirect.Url);
    }

    [Fact]
    public async Task When_SiteIsOffline_RequireCapabilityFilter_BlocksAjaxWithJsonForbidden()
    {
        var runtime = CreateAllEnabledRuntime();
        var siteState = new FakeSiteRuntimeStateProvider(CreateSiteSnapshot("Offline", DateTimeOffset.UtcNow.AddDays(10)));
        var evaluator = new CapabilityEvaluator(_catalog, runtime, siteState, TimeProvider.System);

        var filter = new RequireCapabilityFilter(evaluator, NullLogger<RequireCapabilityFilter>.Instance, CapabilityFeatureCodes.ProductView);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());

        var nextCalled = false;
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        Assert.False(nextCalled);
        var objResult = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);
    }
}
