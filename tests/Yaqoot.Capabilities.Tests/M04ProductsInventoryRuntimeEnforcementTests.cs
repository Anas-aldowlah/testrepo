using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services;
using YAGOT_2._0.Services.Integration;
using AdminProductsController = Yagot.Areas.Admin.Controllers.ProductsController;
using CartController = Yagot.Controllers.CartController;
using QuickSalesController = YAGOT_2._0.Areas.Admin.Controllers.QuickSalesController;
using StorefrontProductsController = YAGOT_2._0.Controllers.ProductsController;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class M04ProductsInventoryRuntimeEnforcementTests
{
    private static readonly string[] M04Features =
    [
        CapabilityFeatureCodes.ProductCreate,
        CapabilityFeatureCodes.ProductView,
        CapabilityFeatureCodes.ProductEdit,
        CapabilityFeatureCodes.ProductDelete,
        CapabilityFeatureCodes.ProductBrands,
        CapabilityFeatureCodes.ProductSearchFilter,
        CapabilityFeatureCodes.InventoryManagement,
        CapabilityFeatureCodes.RetailSelling
    ];

    private readonly CapabilityCatalog _catalog = new();

    [Fact]
    public void EnabledModule_AllowsEachEnabledM04Feature()
    {
        var evaluator = Evaluator(Snapshot());

        Assert.True(evaluator.IsModuleEnabled(CapabilityModuleCodes.ProductsInventory));
        Assert.All(M04Features, code => Assert.True(evaluator.IsFeatureEnabled(code)));
    }

    [Fact]
    public void DisabledModule_DisablesAllEightM04Features()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));

        Assert.False(evaluator.IsModuleEnabled(CapabilityModuleCodes.ProductsInventory));
        Assert.All(M04Features, code =>
            Assert.Equal(CapabilityEvaluationReason.ModuleDisabled, evaluator.EvaluateFeature(code).Reason));
    }

    [Theory]
    [MemberData(nameof(IndependentFeatureCases))]
    public void DisabledFeature_DoesNotDisableOtherM04Features(string disabledFeature, string enabledFeature)
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: disabledFeature));

        Assert.Equal(CapabilityEvaluationReason.FeatureDisabled, evaluator.EvaluateFeature(disabledFeature).Reason);
        Assert.True(evaluator.IsFeatureEnabled(enabledFeature));
    }

    [Fact]
    public async Task ProductViewOff_BlocksAllDirectStorefrontProductRequestsBeforeQueries()
    {
        var controller = new StorefrontProductsController(
            null!, null!, Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.ProductView)));

        Assert.IsType<NotFoundResult>(await controller.Index(new ProductsCatalogRequest(), CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Details(1));
        Assert.IsType<NotFoundResult>(await controller.RecentlyViewed([1]));
    }

    [Fact]
    public async Task ProductViewOff_BlocksDirectAdminProductRequestsBeforeQueries()
    {
        var controller = AdminController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.ProductView)));

        Assert.IsType<ForbidResult>(await controller.Index(null));
        Assert.IsType<ForbidResult>(await controller.trash());
    }

    [Fact]
    public async Task ProductCreateOff_BlocksDirectAdminGetAndPostBeforeMutation()
    {
        var controller = AdminController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.ProductCreate)));

        Assert.IsType<ForbidResult>(await controller.Create());
        Assert.IsType<ForbidResult>(await controller.Create(new AdminProductCreateViewModel()));
    }

    [Fact]
    public async Task ProductEditOff_BlocksDirectAdminGetAndPostBeforeMutation()
    {
        var controller = AdminController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.ProductEdit)));

        Assert.IsType<ForbidResult>(await controller.Edit(1));
        Assert.IsType<ForbidResult>(await controller.Edit(new ProductVW { Id = 1 }));
        Assert.IsType<ForbidResult>(await controller.RefreshBestSellers());
    }

    [Fact]
    public async Task ProductDeleteOff_BlocksDirectAdminPostBeforeMutation()
    {
        var controller = AdminController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.ProductDelete)));

        Assert.IsType<ForbidResult>(await controller.Delete(1));
    }

    [Fact]
    public async Task ProductBrandsOff_BlocksDirectBrandCreationBeforeMutation()
    {
        var controller = AdminController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.ProductBrands)));

        Assert.IsType<ForbidResult>(await controller.Create(new AdminProductCreateViewModel { Brand = "blocked" }));
        Assert.IsType<ForbidResult>(await controller.Edit(new ProductVW { Id = 1, Brand = "blocked" }));
    }

    [Fact]
    public async Task InventoryManagementOff_BlocksStockAdjustmentBeforeServiceCall()
    {
        var controller = AdminController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.InventoryManagement)));

        Assert.IsType<ForbidResult>(await controller.AdjustStock(new StockAdjustmentViewModel()));
    }

    [Fact]
    public async Task RetailSellingOff_BlocksProductRetailConfigurationBeforeMutation()
    {
        var controller = AdminController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.RetailSelling)));

        Assert.IsType<ForbidResult>(await controller.Create(new AdminProductCreateViewModel { IsRetailEnabled = true }));
        Assert.IsType<ForbidResult>(await controller.Edit(new ProductVW { Id = 1, IsRetailEnabled = true }));
    }

    [Fact]
    public async Task RetailSellingOff_BlocksRetailCartMutationBeforeCartServices()
    {
        var controller = new CartController(
            null!, null!, NullLogger<CartController>.Instance,
            Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.RetailSelling)));

        Assert.IsType<ForbidResult>(await controller.Add(new AddCartItemInput { ProductId = 1, Quantity = 1, RetailPriceId = 2 }));
        Assert.IsType<ForbidResult>(await controller.Update(new UpdateCartItemInput { ProductId = 1, Quantity = 1, RetailPriceId = 2 }));
    }

    [Fact]
    public async Task RetailSellingOff_BlocksRetailPosDraftAndCompletionWithoutDisablingPos()
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.RetailSelling));
        var controller = new QuickSalesController(
            null!, null!, null!, NullLogger<QuickSalesController>.Instance, evaluator);
        var item = new SaveDraftItemModel { ProductId = 1, Quantity = 1, RetailPriceId = 2 };

        Assert.IsType<ForbidResult>(await controller.SaveDraft(new SaveDraftRequestModel { Items = [item] }));
        Assert.IsType<ForbidResult>(await controller.CompleteSale(new CompleteSaleRequestModel { Items = [item] }));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.PosCashier));
    }

    [Fact]
    public async Task RetailSellingOff_BlocksRetailValidationBeforeDatabaseQuery()
    {
        var service = new InventoryService(
            null!, Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.RetailSelling)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ValidateRetailPriceAsync(new Product { Id = 1 }, 2));
        Assert.Equal("Retail selling capability is disabled.", exception.Message);
    }

    [Fact]
    public void M04ModuleOff_DoesNotChangeOtherModuleEvaluation()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));

        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CategoryView));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CustomerLogin));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.OrderCreate));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.PosCashier));
    }

    [Fact]
    public void RazorUi_UsesM04CapabilitiesForRelevantControls()
    {
        var root = FindSolutionRoot();

        AssertFileContains(root, Path.Combine("Areas", "Admin", "Views", "Products", "Index.cshtml"),
            "ProductCreate", "ProductEdit", "ProductDelete", "ProductBrands", "ProductSearchFilter", "InventoryManagement");
        AssertFileContains(root, Path.Combine("Areas", "Admin", "Views", "Products", "Create.cshtml"),
            "ProductBrands", "RetailSelling");
        AssertFileContains(root, Path.Combine("Areas", "Admin", "Views", "Products", "Edit.cshtml"),
            "ProductBrands", "InventoryManagement", "RetailSelling");
        AssertFileContains(root, Path.Combine("Views", "Shared", "_StorefrontHeader.cshtml"),
            "ProductView", "ProductSearchFilter");
        AssertFileContains(root, Path.Combine("Views", "Products", "Index.cshtml"),
            "ProductBrands", "ProductSearchFilter", "RetailSelling");
        AssertFileContains(root, Path.Combine("Views", "Products", "Details.cshtml"),
            "ProductBrands", "ProductSearchFilter");
    }

    public static TheoryData<string, string> IndependentFeatureCases => new()
    {
        { CapabilityFeatureCodes.ProductCreate, CapabilityFeatureCodes.ProductView },
        { CapabilityFeatureCodes.ProductView, CapabilityFeatureCodes.ProductEdit },
        { CapabilityFeatureCodes.ProductEdit, CapabilityFeatureCodes.ProductDelete },
        { CapabilityFeatureCodes.ProductDelete, CapabilityFeatureCodes.ProductBrands },
        { CapabilityFeatureCodes.ProductBrands, CapabilityFeatureCodes.ProductSearchFilter },
        { CapabilityFeatureCodes.ProductSearchFilter, CapabilityFeatureCodes.InventoryManagement },
        { CapabilityFeatureCodes.InventoryManagement, CapabilityFeatureCodes.RetailSelling },
        { CapabilityFeatureCodes.RetailSelling, CapabilityFeatureCodes.ProductCreate }
    };

    private AdminProductsController AdminController(ICapabilityEvaluator evaluator) =>
        new(null!, null!, null!, null!, null!, null!, null!, NullLogger<AdminProductsController>.Instance, evaluator);

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
                module.Code != CapabilityModuleCodes.ProductsInventory || moduleEnabled)).ToArray(),
            _catalog.Features.Select(feature => new CapabilityFeatureStateV1(
                feature.Code,
                feature.ModuleCode,
                !string.Equals(feature.Code, disabledFeature, StringComparison.Ordinal))).ToArray());
    }

    private static void AssertFileContains(string root, string relativePath, params string[] values)
    {
        var contents = File.ReadAllText(Path.Combine(root, relativePath));
        Assert.All(values, value => Assert.Contains(value, contents));
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "YAGOT_2.0.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the Yaqoot solution root.");
    }
}
