using System.Reflection;
using System.Security.Claims;
using Directing.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class LocalSiteEnforcementPipelineTests
{
    [Fact]
    public void ControllerCoverage_RemainsExactlyOnApprovedControllers()
    {
        var assembly = typeof(Yagot.Controllers.HomeController).Assembly;
        var controllers = assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } &&
                           typeof(ControllerBase).IsAssignableFrom(type))
            .ToArray();

        var storefront = FilteredBy<SiteStatusFilter>(controllers);
        Assert.Equal(
            new[]
            {
                "YAGOT_2._0.Controllers.OrdersController",
                "YAGOT_2._0.Controllers.ProductsController",
                "Yagot.Controllers.CartController",
                "Yagot.Controllers.HomeController"
            },
            storefront);

        var admin = FilteredBy<SiteStatusFilterAdmin>(controllers);
        Assert.Equal(
            new[]
            {
                "YAGOT_2._0.Areas.Admin.Controllers.CategoriesController",
                "YAGOT_2._0.Areas.Admin.Controllers.DashboardController",
                "YAGOT_2._0.Areas.Admin.Controllers.QuickSalesController",
                "YAGOT_2._0.Areas.Admin.Controllers.SettingsController",
                "Yagot.Areas.Admin.Controllers.ProductsController",
                "Yagot.Areas.Admin.Controllers.UsersController"
            },
            admin);

        Assert.DoesNotContain(
            controllers,
            type => type == typeof(YAGOT_2._0.Controllers.AccountController) && HasEitherFilter(type));
        Assert.DoesNotContain(
            controllers,
            type => type == typeof(YAGOT_2._0.Controllers.CategoriesController) && HasEitherFilter(type));
        Assert.DoesNotContain(
            controllers,
            type => type == typeof(DirectiveDevCloseController) && HasEitherFilter(type));
        Assert.DoesNotContain(
            controllers,
            type => type.FullName == "YAGOT_2._0.Areas.Admin.Controllers.OrdersController" && HasEitherFilter(type));
        Assert.DoesNotContain(
            controllers,
            type => type == typeof(YAGOT_2._0.Controllers.Api.SiteStateIntegrationController) && HasEitherFilter(type));
    }

    [Fact]
    public void Filters_HaveOnlyLocalDecisionServiceDependency()
    {
        foreach (var type in new[] { typeof(SiteStatusFilter), typeof(SiteStatusFilterAdmin) })
        {
            var constructor = Assert.Single(type.GetConstructors());
            var parameter = Assert.Single(constructor.GetParameters());
            Assert.Equal(typeof(ISiteAccessDecisionService), parameter.ParameterType);
        }
    }

    [Fact]
    public void ProductionPipeline_CutoverAndSecurityOrderingArePreserved()
    {
        var source = ReadProgramSource();

        Assert.DoesNotContain(
            "app.UseLegacySiteStatusWithWebhookBypass();",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "builder.Services.AddScoped<ISiteAccessDecisionService, SiteAccessDecisionService>();",
            source,
            StringComparison.Ordinal);
        Assert.Contains("builder.Services.AddScoped<SiteStatusFilter>();", source, StringComparison.Ordinal);
        Assert.Contains("builder.Services.AddScoped<SiteStatusFilterAdmin>();", source, StringComparison.Ordinal);
        Assert.Contains("builder.Services.AddTransient<DealingAPI>();", source, StringComparison.Ordinal);
        Assert.Contains("builder.Services.AddScoped<IVisitService, VisitService>();", source, StringComparison.Ordinal);
        Assert.Contains("builder.Services.AddSiteStateReconciliation(builder.Configuration);", source, StringComparison.Ordinal);

        var staticFiles = Position(source, "app.UseStaticFiles(");
        var webhook = Position(source, "app.UseSiteStateWebhookProtocol();");
        var session = Position(source, "app.UseSession();");
        var authentication = Position(source, "app.UseAuthentication();");
        var adminGate = Position(source, "if (path.StartsWithSegments(\"/Admin\"");
        var authorization = Position(source, "app.UseAuthorization();");
        var staticAssets = Position(source, "app.MapStaticAssets();");

        Assert.True(staticFiles < webhook);
        Assert.True(webhook < session);
        Assert.True(session < authentication);
        Assert.True(authentication < adminGate);
        Assert.True(adminGate < authorization);
        Assert.True(authorization < staticAssets);
        Assert.Contains("StatusCodes.Status401Unauthorized", source, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status403Forbidden", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionService_DependsOnlyOnLocalRuntimeAndFrameworkServices()
    {
        var constructor = Assert.Single(typeof(SiteAccessDecisionService).GetConstructors());
        Assert.Equal(
            new[]
            {
                typeof(ILocalSiteRuntimeStateProvider),
                typeof(TimeProvider),
                typeof(Microsoft.Extensions.Logging.ILogger<SiteAccessDecisionService>)
            },
            constructor.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void LegacyStatusExecutablePath_IsAbsentFromProductionSourceAndConfiguration()
    {
        var root = FindRepositoryRoot();
        var source = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => IsProductionSource(root, path))
            .Append(Path.Combine(root, "appsettings.json"))
            .Select(File.ReadAllText)
            .ToArray();
        var forbidden = new[]
        {
            "checkDeveloperMode",
            "SiteAPI/GetSites",
            "SiteAPI/GetSitesAdmin",
            "SiteDto",
            "SiteDtoAdmin",
            "StatueSite",
            "YQ_SiteStatus",
            "Items[\"SiteStatus\"]",
            "UseLegacySiteStatusWithWebhookBypass",
            "AddHttpClient<DealingAPI>",
            "ExternalApi:BaseUrl"
        };

        foreach (var marker in forbidden)
        {
            Assert.DoesNotContain(
                source,
                content => content.Contains(marker, StringComparison.Ordinal));
        }

        Assert.False(File.Exists(Path.Combine(root, "Models", "SiteDto.cs")));
        Assert.False(File.Exists(Path.Combine(root, "Models", "SiteDtoAdmin.cs")));
        Assert.Equal(
            [nameof(DealingAPI.DecryptPhone)],
            typeof(DealingAPI).GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => method.Name));
    }

    [Fact]
    public async Task CloseRoute_RendersAuthoritativeLocalMetadataOnlyForOffline()
    {
        var metadata = new SiteMaintenanceViewModel(
            "YAGOT",
            "https://example.test",
            new Uri("https://example.test"),
            new DateOnly(2026, 9, 7),
            new DateOnly(2026, 10, 7),
            30);
        var controller = Controller(new SiteAccessDecision(
            SiteAccessDecisionKind.OfflineRestricted,
            SiteAccessHtmlTarget.Close,
            SiteStateContractV1.Offline,
            SiteStateContractTests.ValidSnapshot(mode: SiteStateContractV1.Offline),
            metadata));

        var result = Assert.IsType<ViewResult>(await controller.Close(default));

        Assert.Equal("close", result.ViewName);
        Assert.Same(metadata, result.Model);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
        var parameter = Assert.Single(typeof(DirectiveDevCloseController)
            .GetMethod(nameof(DirectiveDevCloseController.Close))!
            .GetParameters());
        Assert.Equal(typeof(CancellationToken), parameter.ParameterType);
    }

    [Theory]
    [InlineData(SiteStateContractV1.Online, "Index", "Home")]
    [InlineData(SiteStateContractV1.Development, "Developer", null)]
    public async Task CloseRoute_AvoidsLoopsWhenStateChanges(
        string mode,
        string action,
        string? controllerName)
    {
        var controller = Controller(new SiteAccessDecision(
            SiteAccessDecisionKind.Allow,
            SiteAccessHtmlTarget.None,
            mode,
            SiteStateContractTests.ValidSnapshot(mode: mode),
            null));

        var result = Assert.IsType<RedirectToActionResult>(await controller.Close(default));

        Assert.Equal(action, result.ActionName);
        Assert.Equal(controllerName, result.ControllerName);
    }

    [Fact]
    public async Task CloseRoute_MissingReturns503NoStore()
    {
        var controller = Controller(new SiteAccessDecision(
            SiteAccessDecisionKind.Missing,
            SiteAccessHtmlTarget.Unavailable,
            null,
            null,
            null));

        var result = Assert.IsType<ViewResult>(await controller.Close(default));

        Assert.Equal("Unavailable", result.ViewName);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, controller.Response.StatusCode);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    private static DirectiveDevCloseController Controller(SiteAccessDecision decision)
    {
        var controller = new DirectiveDevCloseController(new StubDecisionService(decision));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
        return controller;
    }

    private static string[] FilteredBy<TFilter>(IEnumerable<Type> controllers) =>
        controllers
            .Where(type => type.GetCustomAttributes<ServiceFilterAttribute>()
                .Any(attribute => attribute.ServiceType == typeof(TFilter)))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static bool HasEitherFilter(Type type) =>
        type.GetCustomAttributes<ServiceFilterAttribute>().Any(attribute =>
            attribute.ServiceType == typeof(SiteStatusFilter) ||
            attribute.ServiceType == typeof(SiteStatusFilterAdmin));

    private static int Position(string source, string value)
    {
        var position = source.IndexOf(value, StringComparison.Ordinal);
        Assert.True(position >= 0, $"Program.cs is missing expected pipeline marker: {value}");
        return position;
    }

    private static string ReadProgramSource()
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Program.cs"));
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

    private static bool IsProductionSource(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return !relative.StartsWith(
                   "tests" + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase)
               && !relative.Split(Path.DirectorySeparatorChar).Any(segment =>
                   segment is "bin" or "obj" or ".git");
    }

    private sealed class StubDecisionService(SiteAccessDecision decision)
        : ISiteAccessDecisionService
    {
        public Task<SiteAccessDecision> DecideAsync(
            SiteAccessSurface surface,
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default) => Task.FromResult(decision);
    }
}
