using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services.Integration;
using QuickSalesController = YAGOT_2._0.Areas.Admin.Controllers.QuickSalesController;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class M08SalesPosRuntimeEnforcementTests
{
    private static readonly string[] M08Features =
    [
        CapabilityFeatureCodes.PosCashier,
        CapabilityFeatureCodes.PosSalesDay,
        CapabilityFeatureCodes.PosInvoices,
        CapabilityFeatureCodes.PosDrafts,
        CapabilityFeatureCodes.PosDiscounts,
        CapabilityFeatureCodes.PosMultiplePayments,
        CapabilityFeatureCodes.PosSalesLedger,
        CapabilityFeatureCodes.PosReports
    ];

    private readonly CapabilityCatalog _catalog = new();

    [Fact]
    public void EnabledModule_AllowsEachEnabledM08Feature()
    {
        var evaluator = Evaluator(Snapshot());

        Assert.True(evaluator.IsModuleEnabled(CapabilityModuleCodes.SalesPos));
        Assert.All(M08Features, code => Assert.True(evaluator.IsFeatureEnabled(code)));
    }

    [Fact]
    public void DisabledModule_DisablesAllEightM08Features()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));

        Assert.False(evaluator.IsModuleEnabled(CapabilityModuleCodes.SalesPos));
        Assert.All(M08Features, code =>
            Assert.Equal(CapabilityEvaluationReason.ModuleDisabled, evaluator.EvaluateFeature(code).Reason));
    }

    [Theory]
    [MemberData(nameof(IndependentFeatureCases))]
    public void DisabledFeature_DoesNotDisableOtherM08Features(string disabledFeature, string enabledFeature)
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: disabledFeature));

        Assert.Equal(CapabilityEvaluationReason.FeatureDisabled, evaluator.EvaluateFeature(disabledFeature).Reason);
        Assert.True(evaluator.IsFeatureEnabled(enabledFeature));
    }

    [Fact]
    public async Task ModuleOff_BlocksEveryImplementedPosBoundaryBeforeDependencies()
    {
        var controller = Controller(Evaluator(Snapshot(moduleEnabled: false)));

        Assert.IsType<ForbidResult>(await controller.Index());
        Assert.IsType<ForbidResult>(await controller.OpenDay());
        Assert.IsType<ForbidResult>(await controller.OpenDay(new OpenSalesDayViewModel()));
        Assert.IsType<ForbidResult>(await controller.NewSale(null));
        Assert.IsType<ForbidResult>(await controller.SearchProducts("tea"));
        Assert.IsType<ForbidResult>(await controller.SearchCustomers("customer"));
        Assert.IsType<ForbidResult>(await controller.SaveDraft(new SaveDraftRequestModel()));
        Assert.IsType<ForbidResult>(await controller.GetPaymentMethods());
        Assert.IsType<ForbidResult>(await controller.CompleteSale(new CompleteSaleRequestModel()));
        Assert.IsType<ForbidResult>(await controller.Ledger(null));
        Assert.IsType<ForbidResult>(await controller.SaleDetails(1));
        Assert.IsType<ForbidResult>(await controller.CloseDay(new CloseSalesDayRequestModel()));
        Assert.IsType<ForbidResult>(await controller.History());
        Assert.IsType<ForbidResult>(await controller.Reports(null, null));
    }

    [Fact]
    public async Task CashierOff_BlocksPageSearchAndSaleMutationBeforeDatabaseOrInventory()
    {
        var controller = Controller(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.PosCashier)));

        Assert.IsType<ForbidResult>(await controller.Index());
        Assert.IsType<ForbidResult>(await controller.NewSale(null));
        Assert.IsType<ForbidResult>(await controller.SearchProducts("tea"));
        Assert.IsType<ForbidResult>(await controller.SearchCustomers("customer"));
        Assert.IsType<ForbidResult>(await controller.SaveDraft(new SaveDraftRequestModel()));
        Assert.IsType<ForbidResult>(await controller.CompleteSale(new CompleteSaleRequestModel()));
    }

    [Fact]
    public async Task SalesDayOff_BlocksOpenAndCloseBeforeSessionMutation()
    {
        var controller = Controller(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.PosSalesDay)));

        Assert.IsType<ForbidResult>(await controller.OpenDay());
        Assert.IsType<ForbidResult>(await controller.OpenDay(new OpenSalesDayViewModel()));
        Assert.IsType<ForbidResult>(await controller.CloseDay(new CloseSalesDayRequestModel()));
    }

    [Fact]
    public async Task InvoicesOff_BlocksCompletionBeforeInventoryMutation()
    {
        var controller = Controller(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.PosInvoices)));

        Assert.IsType<ForbidResult>(await controller.NewSale(null));
        Assert.IsType<ForbidResult>(await controller.CompleteSale(new CompleteSaleRequestModel()));
    }

    [Fact]
    public async Task DraftsOff_BlocksCreateSaveRetrieveLockAndDeleteBoundaries()
    {
        var controller = Controller(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.PosDrafts)));

        Assert.IsType<ForbidResult>(await controller.NewSale(null));
        Assert.IsType<ForbidResult>(await controller.SaveDraft(new SaveDraftRequestModel()));
        Assert.IsType<ForbidResult>(await controller.CompleteSale(new CompleteSaleRequestModel()));
        Assert.IsType<ForbidResult>(await controller.AcquireDraftEditSession(new DraftEditSessionRequestModel()));
        Assert.IsType<ForbidResult>(await controller.RenewDraftEditSession(new DraftEditSessionRequestModel()));
        Assert.IsType<ForbidResult>(await controller.ReleaseDraftEditSession(new DraftEditSessionRequestModel()));
        Assert.IsType<ForbidResult>(await controller.DraftState(1));
        Assert.IsType<ForbidResult>(await controller.Drafts());
        Assert.IsType<ForbidResult>(await controller.DeleteDraft(new DeleteDraftRequestModel()));
    }

    [Fact]
    public async Task DiscountsOff_BlocksItemAndInvoiceDiscountPayloadsBeforeMutation()
    {
        var controller = Controller(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.PosDiscounts)));
        var discountedDraft = new SaveDraftRequestModel
        {
            DiscountTotal = 1m,
            Items = [new SaveDraftItemModel { ProductId = 1, Quantity = 1, Discount = 1m }]
        };
        var discountedSale = new CompleteSaleRequestModel
        {
            DiscountTotal = 1m,
            Items = [new SaveDraftItemModel { ProductId = 1, Quantity = 1, Discount = 1m }]
        };

        Assert.IsType<ForbidResult>(await controller.SaveDraft(discountedDraft));
        Assert.IsType<ForbidResult>(await controller.CompleteSale(discountedSale));
    }

    [Fact]
    public async Task MultiplePaymentsOff_BlocksMethodsAndCompletionBeforePaymentWork()
    {
        var controller = Controller(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.PosMultiplePayments)));

        Assert.IsType<ForbidResult>(await controller.GetPaymentMethods());
        Assert.IsType<ForbidResult>(await controller.CompleteSale(new CompleteSaleRequestModel()));
    }

    [Fact]
    public async Task SalesLedgerOff_BlocksHistoryLedgerAndInvoiceDetailsBeforeQueries()
    {
        var controller = Controller(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.PosSalesLedger)));

        Assert.IsType<ForbidResult>(await controller.Ledger(null));
        Assert.IsType<ForbidResult>(await controller.SaleDetails(1));
        Assert.IsType<ForbidResult>(await controller.History());
    }

    [Fact]
    public async Task ReportsOff_BlocksReportBeforeQueries()
    {
        var controller = Controller(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.PosReports)));

        Assert.IsType<ForbidResult>(await controller.Reports(null, null));
    }

    [Fact]
    public void M08ModuleOff_DoesNotDisableM02M04M06OrM07()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));

        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.StoreDashboard));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.ProductView));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.InventoryManagement));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CustomerLogin));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.OrderCreate));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.Checkout));
    }

    [Fact]
    public void RazorUi_UsesAllM08CapabilitiesForRelevantControls()
    {
        var root = FindSolutionRoot();

        AssertFileContains(root, Path.Combine("Areas", "Admin", "Views", "Shared", "_AdminLayout.cshtml"),
            "PosCashier", "PosReports");
        AssertFileContains(root, Path.Combine("Areas", "Admin", "Views", "QuickSales", "Index.cshtml"),
            "PosSalesDay", "PosInvoices", "PosDrafts", "PosSalesLedger");
        AssertFileContains(root, Path.Combine("Areas", "Admin", "Views", "QuickSales", "NewSale.cshtml"),
            "PosDiscounts", "PosMultiplePayments", "PosInvoices");
        AssertFileContains(root, Path.Combine("Areas", "Admin", "Views", "QuickSales", "Ledger.cshtml"),
            "PosCashier", "PosDrafts", "PosInvoices", "PosSalesDay");
    }

    [Fact]
    public void SharedServicesAndCustomerOrderControllersHaveNoM08Gate()
    {
        var root = FindSolutionRoot();
        var paths = new[]
        {
            Path.Combine("Services", "InventoryService.cs"),
            Path.Combine("Services", "OrderService.cs"),
            Path.Combine("Services", "ReceiptStorageService.cs"),
            Path.Combine("Services", "ProductCatalogService.cs"),
            Path.Combine("Services", "StoreSettingsService.cs"),
            Path.Combine("Controllers", "OrdersController.cs")
        };

        Assert.All(paths, path => AssertFileDoesNotContain(root, path, "SalesPos", "PosCashier", "PosInvoices"));
    }

    public static TheoryData<string, string> IndependentFeatureCases => new()
    {
        { CapabilityFeatureCodes.PosCashier, CapabilityFeatureCodes.PosSalesDay },
        { CapabilityFeatureCodes.PosSalesDay, CapabilityFeatureCodes.PosInvoices },
        { CapabilityFeatureCodes.PosInvoices, CapabilityFeatureCodes.PosDrafts },
        { CapabilityFeatureCodes.PosDrafts, CapabilityFeatureCodes.PosDiscounts },
        { CapabilityFeatureCodes.PosDiscounts, CapabilityFeatureCodes.PosMultiplePayments },
        { CapabilityFeatureCodes.PosMultiplePayments, CapabilityFeatureCodes.PosSalesLedger },
        { CapabilityFeatureCodes.PosSalesLedger, CapabilityFeatureCodes.PosReports },
        { CapabilityFeatureCodes.PosReports, CapabilityFeatureCodes.PosCashier }
    };

    private static QuickSalesController Controller(ICapabilityEvaluator evaluator) =>
        new(null!, null!, null!, NullLogger<QuickSalesController>.Instance, evaluator);

    private CapabilityEvaluator Evaluator(CapabilitySnapshotV1 snapshot)
    {
        var runtime = new LocalCapabilityRuntimeStateProvider(_catalog, TimeProvider.System);
        runtime.Publish(snapshot);
        return new CapabilityEvaluator(_catalog, runtime);
    }

    private CapabilitySnapshotV1 Snapshot(bool moduleEnabled = true, string? disabledFeature = null)
    {
        var now = new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero);
        return new CapabilitySnapshotV1(
            CapabilityContractV1.ContractVersion,
            CapabilityContractV1.CatalogVersion,
            1,
            1,
            now,
            now,
            _catalog.Modules.Select(module => new CapabilityModuleStateV1(
                module.Code,
                module.Code != CapabilityModuleCodes.SalesPos || moduleEnabled)).ToArray(),
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

    private static void AssertFileDoesNotContain(string root, string relativePath, params string[] values)
    {
        var contents = File.ReadAllText(Path.Combine(root, relativePath));
        Assert.All(values, value => Assert.DoesNotContain(value, contents));
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "YAGOT_2.0.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the Yaqoot solution root.");
    }
}
