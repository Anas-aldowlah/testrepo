using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;
using YAGOT_2._0.Services.Integration;
using AdminOrdersController = YAGOT_2._0.Areas.Admin.Controllers.OrdersController;
using CartController = Yagot.Controllers.CartController;
using CustomerOrdersController = YAGOT_2._0.Controllers.OrdersController;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class M07MarketingOrdersRuntimeEnforcementTests
{
    private static readonly string[] M07Features =
    [
        CapabilityFeatureCodes.CartProductManagement,
        CapabilityFeatureCodes.CartView,
        CapabilityFeatureCodes.OrderCreate,
        CapabilityFeatureCodes.Checkout,
        CapabilityFeatureCodes.OrderManagement,
        CapabilityFeatureCodes.OrderPaymentProof
    ];

    private readonly CapabilityCatalog _catalog = new();

    [Fact]
    public void EnabledModule_AllowsEachEnabledM07Feature()
    {
        var evaluator = Evaluator(Snapshot());

        Assert.True(evaluator.IsModuleEnabled(CapabilityModuleCodes.MarketingOrders));
        Assert.All(M07Features, code => Assert.True(evaluator.IsFeatureEnabled(code)));
    }

    [Fact]
    public void DisabledModule_DisablesAllSixM07Features()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));

        Assert.False(evaluator.IsModuleEnabled(CapabilityModuleCodes.MarketingOrders));
        Assert.All(M07Features, code =>
            Assert.Equal(CapabilityEvaluationReason.ModuleDisabled, evaluator.EvaluateFeature(code).Reason));
    }

    [Theory]
    [MemberData(nameof(IndependentFeatureCases))]
    public void DisabledFeature_DoesNotDisableOtherM07Features(string disabledFeature, string enabledFeature)
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: disabledFeature));

        Assert.Equal(CapabilityEvaluationReason.FeatureDisabled, evaluator.EvaluateFeature(disabledFeature).Reason);
        Assert.True(evaluator.IsFeatureEnabled(enabledFeature));
    }

    [Fact]
    public async Task CartViewOff_BlocksDirectGetBeforeCartQueries()
    {
        var controller = CartController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CartView)));

        Assert.IsType<NotFoundResult>(await controller.Index());
    }

    [Fact]
    public async Task CartProductManagementOff_BlocksAllImplementedCartMutationsBeforeServices()
    {
        var controller = CartController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CartProductManagement)));

        Assert.IsType<NotFoundResult>(await controller.Add(new AddCartItemInput()));
        Assert.IsType<NotFoundResult>(await controller.Update(new UpdateCartItemInput()));
        Assert.IsType<NotFoundResult>(await controller.Remove(1, 1, null));
    }

    [Fact]
    public async Task CheckoutOff_BlocksGetPostAndDraftEndpointsBeforeDependencies()
    {
        var controller = CustomerOrdersController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.Checkout)));

        Assert.IsType<NotFoundResult>(await controller.Checkout());
        Assert.IsType<NotFoundResult>(await controller.CheckoutPost(new CheckoutVM()));
        Assert.IsType<NotFoundResult>(controller.SaveCheckoutDraft(new CheckoutDraftState()));
        Assert.IsType<NotFoundResult>(controller.ClearCheckoutDraft());
    }

    [Fact]
    public async Task OrderCreateOff_BlocksCheckoutSubmissionAndServiceBeforeCartOrInventoryWork()
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.OrderCreate));
        var controller = CustomerOrdersController(evaluator);
        var service = new OrderService(
            null!, null!, null!, NullLogger<OrderService>.Instance, evaluator);

        Assert.IsType<NotFoundResult>(await controller.CheckoutPost(new CheckoutVM()));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrderAsync(1, null!));
        Assert.Equal("Order creation capability is disabled.", exception.Message);
    }

    [Fact]
    public async Task OrderManagementOff_BlocksCustomerTrackingAndConflictMutationBeforeQueries()
    {
        var controller = CustomerOrdersController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.OrderManagement)));

        Assert.IsType<NotFoundResult>(await controller.Index());
        Assert.IsType<NotFoundResult>(await controller.Details(1));
        Assert.IsType<NotFoundResult>(await controller.ResolveInventoryConflict(new ResolveInventoryConflictRequest()));
    }

    [Fact]
    public async Task OrderManagementOff_BlocksEveryAdminOrderBoundaryBeforeQueriesOrMutations()
    {
        var controller = AdminOrdersController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.OrderManagement)));

        Assert.IsType<ForbidResult>(await controller.Index(null, null));
        Assert.IsType<ForbidResult>(await controller.Details(1));
        Assert.IsType<ForbidResult>(await controller.Receipt(1));
        Assert.IsType<ForbidResult>(await controller.UpdateStatus(1, OrderStatuses.Paid));
        Assert.IsType<ForbidResult>(await controller.UpdateAdminNote(1, null));
        Assert.IsType<ForbidResult>(await controller.VerifyPayment(1));
    }

    [Fact]
    public async Task PaymentProofOff_BlocksCustomerUploadAndAdminProofActionsBeforeStorageOrDatabaseWork()
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.OrderPaymentProof));
        var customer = CustomerOrdersController(evaluator);
        var admin = AdminOrdersController(evaluator);
        var service = new OrderService(
            null!, null!, null!, NullLogger<OrderService>.Instance, evaluator);
        var upload = new FormFile(Stream.Null, 0, 1, "ReceiptImage", "receipt.png");

        Assert.IsType<NotFoundResult>(await customer.CheckoutPost(new CheckoutVM { ReceiptImage = upload }));
        Assert.IsType<ForbidResult>(await admin.Receipt(1));
        Assert.IsType<ForbidResult>(await admin.VerifyPayment(1));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrderAsync(1, null!, "receipts/blocked.png"));
        Assert.Equal("Order payment proof capability is disabled.", exception.Message);
    }

    [Fact]
    public async Task OrderCreateOff_BlocksDirectConfirmationButOrderManagementRemainsIndependent()
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.OrderCreate));
        var controller = CustomerOrdersController(evaluator);

        Assert.IsType<NotFoundResult>(await controller.Confirmation(1));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.OrderManagement));
    }

    [Fact]
    public void M07ModuleOff_DoesNotDisableProductBrowsingAccountsInventoryOrPos()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));

        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.ProductView));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.InventoryManagement));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CustomerLogin));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.PosCashier));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.PosMultiplePayments));
    }

    [Fact]
    public void RazorUi_UsesM07CapabilitiesForRelevantControls()
    {
        var root = FindSolutionRoot();

        AssertFileContains(root, Path.Combine("Views", "Shared", "_StorefrontHeader.cshtml"),
            "CartView", "OrderManagement");
        AssertFileContains(root, Path.Combine("Views", "Shared", "_ProductCard.cshtml"), "CartProductManagement");
        AssertFileContains(root, Path.Combine("Views", "Products", "Details.cshtml"), "CartProductManagement");
        AssertFileContains(root, Path.Combine("Views", "Cart", "Index.cshtml"),
            "CartProductManagement", "Checkout", "OrderCreate");
        AssertFileContains(root, Path.Combine("Views", "Orders", "Checkout.cshtml"),
            "OrderCreate", "OrderPaymentProof");
        AssertFileContains(root, Path.Combine("Areas", "Admin", "Views", "Orders", "Index.cshtml"),
            "OrderPaymentProof");
    }

    [Fact]
    public void SharedInventoryAuthenticationAndPosPathsHaveNoM07Gate()
    {
        var root = FindSolutionRoot();

        AssertFileDoesNotContain(root, Path.Combine("Services", "InventoryService.cs"), "MarketingOrders", "OrderCreate", "Checkout");
        AssertFileDoesNotContain(root, Path.Combine("Controllers", "AccountController.cs"), "MarketingOrders", "OrderCreate", "Checkout");
        AssertFileDoesNotContain(root, Path.Combine("Areas", "Admin", "Controllers", "QuickSalesController.cs"),
            "MarketingOrders", "OrderCreate", "Checkout", "OrderManagement", "OrderPaymentProof");
    }

    public static TheoryData<string, string> IndependentFeatureCases => new()
    {
        { CapabilityFeatureCodes.CartProductManagement, CapabilityFeatureCodes.CartView },
        { CapabilityFeatureCodes.CartView, CapabilityFeatureCodes.OrderCreate },
        { CapabilityFeatureCodes.OrderCreate, CapabilityFeatureCodes.Checkout },
        { CapabilityFeatureCodes.Checkout, CapabilityFeatureCodes.OrderManagement },
        { CapabilityFeatureCodes.OrderManagement, CapabilityFeatureCodes.OrderPaymentProof },
        { CapabilityFeatureCodes.OrderPaymentProof, CapabilityFeatureCodes.CartProductManagement }
    };

    private static CartController CartController(ICapabilityEvaluator evaluator) =>
        new(null!, null!, NullLogger<CartController>.Instance, evaluator);

    private static CustomerOrdersController CustomerOrdersController(ICapabilityEvaluator evaluator) =>
        new(null!, null!, null!, null!, null!, null!, null!, NullLogger<CustomerOrdersController>.Instance, null!, evaluator);

    private static AdminOrdersController AdminOrdersController(ICapabilityEvaluator evaluator) =>
        new(null!, null!, null!, null!, null!, evaluator);

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
                module.Code != CapabilityModuleCodes.MarketingOrders || moduleEnabled)).ToArray(),
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
