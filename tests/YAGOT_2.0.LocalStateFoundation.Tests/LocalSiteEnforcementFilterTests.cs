using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class LocalSiteEnforcementFilterTests
{
    [Theory]
    [InlineData(false, SiteAccessHtmlTarget.Developer, "Developer")]
    [InlineData(true, SiteAccessHtmlTarget.Close, "close")]
    public async Task HtmlRestriction_RedirectsAndDoesNotExecuteAction(
        bool adminFilter,
        SiteAccessHtmlTarget target,
        string action)
    {
        var decision = Restricted(target);
        var executed = false;
        var context = Context();

        await InvokeAsync(adminFilter, decision, context, () => executed = true);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal(action, redirect.ActionName);
        Assert.Equal("DirectiveDevClose", redirect.ControllerName);
        Assert.False(executed);
    }

    [Theory]
    [InlineData("path")]
    [InlineData("xhr")]
    [InlineData("accept")]
    [InlineData("content")]
    public async Task JsonClassification_Returns503NoStoreWithoutExecuting(
        string classification)
    {
        var context = Context();
        switch (classification)
        {
            case "path": context.HttpContext.Request.Path = "/api/catalog"; break;
            case "xhr": context.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest"; break;
            case "accept": context.HttpContext.Request.Headers.Accept = "application/json"; break;
            case "content": context.HttpContext.Request.ContentType = "application/json; charset=utf-8"; break;
        }

        var executed = false;
        await InvokeAsync(
            false,
            Restricted(
                SiteAccessHtmlTarget.Developer,
                SiteAccessDecisionKind.DevelopmentRestricted),
            context,
            () => executed = true);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("no-store", context.HttpContext.Response.Headers.CacheControl);
        Assert.Contains("site_development", System.Text.Json.JsonSerializer.Serialize(result.Value));
        Assert.False(executed);
    }

    [Fact]
    public async Task AllowedDecision_ExecutesAction()
    {
        var context = Context();
        var executed = false;

        await InvokeAsync(
            false,
            new SiteAccessDecision(
                SiteAccessDecisionKind.Allow,
                SiteAccessHtmlTarget.None,
                SiteStateContractV1.Online,
                SiteStateContractTests.ValidSnapshot(),
                null),
            context,
            () => executed = true);

        Assert.Null(context.Result);
        Assert.True(executed);
    }

    [Fact]
    public async Task MissingHtml_Returns503UnavailableView()
    {
        var context = Context();

        await InvokeAsync(
            false,
            new SiteAccessDecision(
                SiteAccessDecisionKind.Missing,
                SiteAccessHtmlTarget.Unavailable,
                null,
                null,
                null),
            context,
            () => Assert.Fail("Denied action executed."));

        var result = Assert.IsType<ViewResult>(context.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("~/Views/DirectiveDevClose/Unavailable.cshtml", result.ViewName);
        Assert.Equal("no-store", context.HttpContext.Response.Headers.CacheControl);
    }

    [Theory]
    [InlineData(SiteAccessDecisionKind.OfflineRestricted, "site_offline")]
    [InlineData(SiteAccessDecisionKind.Missing, "site_state_missing")]
    [InlineData(SiteAccessDecisionKind.StorageUnavailable, "site_state_storage_unavailable")]
    [InlineData(SiteAccessDecisionKind.LoadTimeout, "site_state_read_timeout")]
    [InlineData(SiteAccessDecisionKind.InvalidDurableState, "site_state_invalid")]
    [InlineData(SiteAccessDecisionKind.RevisionRegression, "site_state_revision_regression")]
    [InlineData(SiteAccessDecisionKind.EqualRevisionConflict, "site_state_equal_revision_conflict")]
    [InlineData(SiteAccessDecisionKind.ProviderUnavailable, "site_state_provider_unavailable")]
    public async Task JsonDenial_UsesStableSanitizedCode(
        SiteAccessDecisionKind kind,
        string expectedCode)
    {
        var context = Context();
        context.HttpContext.Request.Path = "/api/test";
        var decision = new SiteAccessDecision(
            kind,
            kind == SiteAccessDecisionKind.OfflineRestricted
                ? SiteAccessHtmlTarget.Developer
                : SiteAccessHtmlTarget.Unavailable,
            kind == SiteAccessDecisionKind.OfflineRestricted
                ? SiteStateContractV1.Offline
                : null,
            null,
            null);

        await InvokeAsync(
            false,
            decision,
            context,
            () => Assert.Fail("Denied action executed."));

        var result = Assert.IsType<ObjectResult>(context.Result);
        var payload = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Contains($"\"code\":\"{expectedCode}\"", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store", context.HttpContext.Response.Headers.CacheControl);
    }

    private static async Task InvokeAsync(
        bool adminFilter,
        SiteAccessDecision decision,
        ActionExecutingContext context,
        Action executed)
    {
        var service = new StubDecisionService(decision);
        IAsyncActionFilter filter = adminFilter
            ? new SiteStatusFilterAdmin(service)
            : new SiteStatusFilter(service);
        await filter.OnActionExecutionAsync(context, () =>
        {
            executed();
            return Task.FromResult(new ActionExecutedContext(
                context,
                context.Filters,
                context.Controller));
        });
    }

    private static ActionExecutingContext Context()
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        var actionContext = new ActionContext(
            http,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        return new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            new object());
    }

    private static SiteAccessDecision Restricted(
        SiteAccessHtmlTarget target,
        SiteAccessDecisionKind kind = SiteAccessDecisionKind.OfflineRestricted) =>
        new(
            kind,
            target,
            kind == SiteAccessDecisionKind.DevelopmentRestricted
                ? SiteStateContractV1.Development
                : SiteStateContractV1.Offline,
            SiteStateContractTests.ValidSnapshot(),
            null);

    private sealed class StubDecisionService(SiteAccessDecision decision)
        : ISiteAccessDecisionService
    {
        public Task<SiteAccessDecision> DecideAsync(
            SiteAccessSurface surface,
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default) => Task.FromResult(decision);
    }
}
