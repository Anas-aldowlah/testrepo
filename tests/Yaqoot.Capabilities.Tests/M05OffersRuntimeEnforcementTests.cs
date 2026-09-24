using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services.Integration;
using YAGOT_2._0.Services.Promotions;
using AdminCategoriesController = YAGOT_2._0.Areas.Admin.Controllers.CategoriesController;
using AdminDashboardController = YAGOT_2._0.Areas.Admin.Controllers.DashboardController;
using AdminProductsController = Yagot.Areas.Admin.Controllers.ProductsController;
using AdminPromotionsController = YAGOT_2._0.Areas.Admin.Controllers.PromotionsController;
using AdminSettingsController = YAGOT_2._0.Areas.Admin.Controllers.SettingsController;
using AdminUsersController = Yagot.Areas.Admin.Controllers.UsersController;
using StorefrontOffersController = YAGOT_2._0.Controllers.OffersController;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class M05OffersRuntimeEnforcementTests
{
    private static readonly string[] M05Features =
    [
        CapabilityFeatureCodes.OfferDuration,
        CapabilityFeatureCodes.OfferManagement,
        CapabilityFeatureCodes.OfferStatus,
        CapabilityFeatureCodes.OfferProducts,
        CapabilityFeatureCodes.OfferCustomerDisplay,
        CapabilityFeatureCodes.OfferDiscountPricing
    ];

    private readonly CapabilityCatalog _catalog = new();

    [Fact]
    public void EnabledModule_AllowsEachEnabledM05Feature()
    {
        var evaluator = Evaluator(Snapshot());

        Assert.True(evaluator.IsModuleEnabled(CapabilityModuleCodes.Offers));
        Assert.All(M05Features, code => Assert.True(evaluator.IsFeatureEnabled(code)));
    }

    [Fact]
    public void DisabledModule_DisablesAllSixM05Features()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));

        Assert.False(evaluator.IsModuleEnabled(CapabilityModuleCodes.Offers));
        Assert.All(M05Features, code =>
            Assert.Equal(CapabilityEvaluationReason.ModuleDisabled, evaluator.EvaluateFeature(code).Reason));
    }

    [Theory]
    [MemberData(nameof(IndependentFeatureCases))]
    public void DisabledFeature_DoesNotDisableOtherM05Features(string disabledFeature, string enabledFeature)
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: disabledFeature));

        Assert.Equal(CapabilityEvaluationReason.FeatureDisabled, evaluator.EvaluateFeature(disabledFeature).Reason);
        Assert.True(evaluator.IsFeatureEnabled(enabledFeature));
    }

    [Fact]
    public async Task OfferDiscountPricing_Disabled_ReturnsEmptyActivePromotions()
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.OfferDiscountPricing));
        var engine = new PromotionEngine(capabilityEvaluator: evaluator);

        var activePromos = await engine.GetActivePromotionsAsync();

        Assert.Empty(activePromos);
    }

    [Fact]
    public async Task OfferDiscountPricing_Disabled_SuppressesAllDiscountsInCalculation()
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.OfferDiscountPricing));
        var engine = new PromotionEngine(capabilityEvaluator: evaluator);

        var context = new PromotionCalculationContext
        {
            Items =
            [
                new PromotionCalculationLineItem
                {
                    LineIdentifier = "item-1",
                    ProductId = 10,
                    UnitPrice = 100m,
                    Quantity = 2
                }
            ]
        };

        var promoListWithDiscount = new List<Promotion>
        {
            new()
            {
                Id = 1,
                Title = "Super 50% Off",
                IsActive = true,
                DiscountValue = 50m,
                PromotionType = "Percentage",
                StartDate = DateTimeOffset.UtcNow.AddDays(-1),
                EndDate = DateTimeOffset.UtcNow.AddDays(1)
            }
        };

        var result = await engine.CalculatePromotionsAsync(context, promoListWithDiscount);

        Assert.Equal(200m, result.GrossSubtotal);
        Assert.Equal(0m, result.TotalDiscounts);
        Assert.Equal(200m, result.NetTotal);
        Assert.Empty(result.AppliedSpendPromotions);
    }

    [Fact]
    public async Task RequireCapabilityFilter_BlocksExecution_WhenFeatureDisabled()
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.OfferManagement));
        var filter = new RequireCapabilityFilter(evaluator, NullLogger<RequireCapabilityFilter>.Instance, CapabilityFeatureCodes.OfferManagement);

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        var nextCalled = false;
        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        }

        await filter.OnActionExecutionAsync(actionExecutingContext, Next);

        Assert.False(nextCalled);
        var redirect = Assert.IsType<RedirectResult>(actionExecutingContext.Result);
        Assert.Contains("403", redirect.Url);
    }

    [Fact]
    public async Task RequireCapabilityFilter_ReturnsJson403_ForAjaxRequests()
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.StoreSettings));
        var filter = new RequireCapabilityFilter(evaluator, NullLogger<RequireCapabilityFilter>.Instance, CapabilityFeatureCodes.StoreSettings);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        var nextCalled = false;
        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        }

        await filter.OnActionExecutionAsync(actionExecutingContext, Next);

        Assert.False(nextCalled);
        var objectResult = Assert.IsType<ObjectResult>(actionExecutingContext.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task RequireCapabilityFilter_AllowsExecution_WhenFeatureEnabled()
    {
        var evaluator = Evaluator(Snapshot());
        var filter = new RequireCapabilityFilter(evaluator, NullLogger<RequireCapabilityFilter>.Instance, CapabilityFeatureCodes.OfferManagement);

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        var nextCalled = false;
        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        }

        await filter.OnActionExecutionAsync(actionExecutingContext, Next);

        Assert.True(nextCalled);
        Assert.Null(actionExecutingContext.Result);
    }

    [Theory]
    [InlineData(typeof(AdminPromotionsController), CapabilityFeatureCodes.OfferManagement)]
    [InlineData(typeof(StorefrontOffersController), CapabilityFeatureCodes.OfferCustomerDisplay)]
    [InlineData(typeof(AdminSettingsController), CapabilityFeatureCodes.StoreSettings)]
    [InlineData(typeof(AdminDashboardController), CapabilityFeatureCodes.StoreDashboard)]
    [InlineData(typeof(AdminCategoriesController), CapabilityFeatureCodes.CategoryView)]
    [InlineData(typeof(AdminProductsController), CapabilityFeatureCodes.ProductView)]
    [InlineData(typeof(AdminUsersController), CapabilityFeatureCodes.StoreUserEmployeeManagement)]
    public void Controllers_AreGuarded_WithRequireCapabilityAttribute(Type controllerType, string expectedCapability)
    {
        var attribute = controllerType.GetCustomAttribute<RequireCapabilityAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(expectedCapability, attribute.Arguments?[0]);
    }

    [Fact]
    public void Views_Contain_OffersCapabilityGuards()
    {
        var root = FindSolutionRoot();
        AssertFileContains(root, "Areas/Admin/Views/Shared/_AdminLayout.cshtml",
            "offersManagementEnabled",
            "asp-controller=\"Promotions\"");

        AssertFileContains(root, "Views/Home/Index.cshtml",
            "offersCustomerDisplayEnabled",
            "yq-home-section--promotions");

        AssertFileContains(root, "Views/Shared/_StorefrontHeader.cshtml",
            "offersCustomerDisplayEnabled",
            "asp-controller=\"Offers\"");
    }

    public static TheoryData<string, string> IndependentFeatureCases => new()
    {
        { CapabilityFeatureCodes.OfferDuration, CapabilityFeatureCodes.OfferManagement },
        { CapabilityFeatureCodes.OfferManagement, CapabilityFeatureCodes.OfferStatus },
        { CapabilityFeatureCodes.OfferStatus, CapabilityFeatureCodes.OfferProducts },
        { CapabilityFeatureCodes.OfferProducts, CapabilityFeatureCodes.OfferCustomerDisplay },
        { CapabilityFeatureCodes.OfferCustomerDisplay, CapabilityFeatureCodes.OfferDiscountPricing },
        { CapabilityFeatureCodes.OfferDiscountPricing, CapabilityFeatureCodes.OfferDuration }
    };

    private CapabilityEvaluator Evaluator(CapabilitySnapshotV1 snapshot)
    {
        var runtime = new LocalCapabilityRuntimeStateProvider(_catalog, TimeProvider.System);
        runtime.Publish(snapshot);
        return new CapabilityEvaluator(_catalog, runtime);
    }

    private CapabilitySnapshotV1 Snapshot(bool moduleEnabled = true, string? disabledFeature = null)
    {
        var now = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
        return new CapabilitySnapshotV1(
            CapabilityContractV1.ContractVersion,
            CapabilityContractV1.CatalogVersion,
            1,
            1,
            now,
            now,
            _catalog.Modules.Select(module => new CapabilityModuleStateV1(
                module.Code,
                module.Code != CapabilityModuleCodes.Offers || moduleEnabled)).ToArray(),
            _catalog.Features.Select(feature => new CapabilityFeatureStateV1(
                feature.Code,
                feature.ModuleCode,
                !string.Equals(feature.Code, disabledFeature, StringComparison.Ordinal))).ToArray());
    }

    private static void AssertFileContains(string root, string relativePath, params string[] values)
    {
        var contents = System.IO.File.ReadAllText(System.IO.Path.Combine(root, relativePath));
        Assert.All(values, value => Assert.Contains(value, contents));
    }

    private static string FindSolutionRoot()
    {
        var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "YAGOT_2.0.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new System.IO.DirectoryNotFoundException("Could not locate the Yaqoot solution root.");
    }
}
