using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services.Integration;
using AdminDashboardController = YAGOT_2._0.Areas.Admin.Controllers.DashboardController;
using AdminSettingsController = YAGOT_2._0.Areas.Admin.Controllers.SettingsController;
using AdminUsersController = Yagot.Areas.Admin.Controllers.UsersController;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class M02StoreManagementRuntimeEnforcementTests
{
    private static readonly string[] M02Features =
    [
        CapabilityFeatureCodes.StoreDashboard,
        CapabilityFeatureCodes.StoreUserEmployeeManagement,
        CapabilityFeatureCodes.StoreRolesPermissions,
        CapabilityFeatureCodes.StoreSettings
    ];

    private readonly CapabilityCatalog _catalog = new();

    [Fact]
    public void EnabledModule_AllowsEachEnabledM02Feature()
    {
        var evaluator = Evaluator(Snapshot());

        Assert.True(evaluator.IsModuleEnabled(CapabilityModuleCodes.StoreManagement));
        Assert.All(M02Features, code => Assert.True(evaluator.IsFeatureEnabled(code)));
    }

    [Fact]
    public void DisabledModule_DisablesAllFourM02Features()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));

        Assert.False(evaluator.IsModuleEnabled(CapabilityModuleCodes.StoreManagement));
        Assert.All(M02Features, code =>
            Assert.Equal(CapabilityEvaluationReason.ModuleDisabled, evaluator.EvaluateFeature(code).Reason));
    }

    [Theory]
    [MemberData(nameof(IndependentFeatureCases))]
    public void DisabledFeature_DoesNotDisableOtherM02Features(string disabledFeature, string enabledFeature)
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: disabledFeature));

        Assert.Equal(CapabilityEvaluationReason.FeatureDisabled, evaluator.EvaluateFeature(disabledFeature).Reason);
        Assert.True(evaluator.IsFeatureEnabled(enabledFeature));
    }

    [Fact]
    public async Task DashboardOff_BlocksDirectGetAndMutationBeforeServices()
    {
        var controller = DashboardController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.StoreDashboard)));

        Assert.IsType<ForbidResult>(await controller.Index());
        Assert.IsType<ForbidResult>(await controller.TriggerManualSync());
    }

    [Fact]
    public async Task UserEmployeeManagementOff_BlocksDirectAccessAndMutationBeforeQueries()
    {
        var controller = UsersController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.StoreUserEmployeeManagement)));

        Assert.IsType<ForbidResult>(await controller.Index());
        Assert.IsType<ForbidResult>(await controller.Details(7));
        Assert.IsType<ForbidResult>(await controller.ToggleBlock(7, true));
    }

    [Fact]
    public async Task RolesPermissionsOff_BlocksDirectRoleMutationBeforeQueries()
    {
        var controller = UsersController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.StoreRolesPermissions)));

        Assert.IsType<ForbidResult>(await controller.ChangeRole(7, "Admin"));
    }

    [Fact]
    public async Task StoreSettingsOff_BlocksDirectGetAndPostBeforeServices()
    {
        var controller = SettingsController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.StoreSettings)));

        Assert.IsType<ForbidResult>(await controller.Index());
        Assert.IsType<ForbidResult>(await controller.Index(new StoreSettings()));
    }

    [Fact]
    public async Task DisabledModule_BlocksEveryExistingM02ControllerBoundary()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));
        var dashboard = DashboardController(evaluator);
        var users = UsersController(evaluator);
        var settings = SettingsController(evaluator);

        Assert.IsType<ForbidResult>(await dashboard.Index());
        Assert.IsType<ForbidResult>(await dashboard.TriggerManualSync());
        Assert.IsType<ForbidResult>(await users.Index());
        Assert.IsType<ForbidResult>(await users.Details(7));
        Assert.IsType<ForbidResult>(await users.ToggleBlock(7, true));
        Assert.IsType<ForbidResult>(await users.ChangeRole(7, "Admin"));
        Assert.IsType<ForbidResult>(await settings.Index());
        Assert.IsType<ForbidResult>(await settings.Index(new StoreSettings()));
    }

    [Fact]
    public void M02ModuleOff_DoesNotChangeOtherModuleEvaluation()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));

        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CoreAuthentication));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CategoryView));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.ProductView));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CustomerLogin));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.OrderManagement));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.PosCashier));
    }

    [Fact]
    public async Task RuntimePublication_IsObservedWithoutRecreatingController()
    {
        var runtime = new LocalCapabilityRuntimeStateProvider(_catalog, TimeProvider.System);
        runtime.Publish(Snapshot(revision: 1));
        var controller = DashboardController(new CapabilityEvaluator(_catalog, runtime));

        runtime.Publish(Snapshot(revision: 2, disabledFeature: CapabilityFeatureCodes.StoreDashboard));

        Assert.IsType<ForbidResult>(await controller.Index());
    }

    [Fact]
    public void RazorUi_UsesM02AndIndependentCrossModuleCapabilities()
    {
        var root = FindSolutionRoot();

        AssertFileContains(root, Path.Combine("Areas", "Admin", "Views", "Shared", "_AdminLayout.cshtml"),
            "StoreDashboard", "StoreUserEmployeeManagement", "StoreSettings",
            "CategoryView", "ProductView", "OrderManagement", "PosCashier", "PosReports");
        AssertFileContains(root, Path.Combine("Areas", "Admin", "Views", "Users", "Details.cshtml"),
            "StoreRolesPermissions", "roleManagementEnabled");
        AssertFileContains(root, Path.Combine("Areas", "Admin", "Views", "Dashboard", "Index.cshtml"),
            "CategoryCreate", "ProductCreate", "ProductView", "OrderManagement");
    }

    public static TheoryData<string, string> IndependentFeatureCases => new()
    {
        { CapabilityFeatureCodes.StoreDashboard, CapabilityFeatureCodes.StoreUserEmployeeManagement },
        { CapabilityFeatureCodes.StoreUserEmployeeManagement, CapabilityFeatureCodes.StoreRolesPermissions },
        { CapabilityFeatureCodes.StoreRolesPermissions, CapabilityFeatureCodes.StoreSettings },
        { CapabilityFeatureCodes.StoreSettings, CapabilityFeatureCodes.StoreDashboard }
    };

    private static AdminDashboardController DashboardController(ICapabilityEvaluator evaluator) =>
        new(null!, null!, null!, null!, NullLogger<AdminDashboardController>.Instance, evaluator);

    private static AdminUsersController UsersController(ICapabilityEvaluator evaluator) =>
        new(null!, null!, null!, evaluator);

    private static AdminSettingsController SettingsController(ICapabilityEvaluator evaluator) =>
        new(null!, null!, NullLogger<AdminSettingsController>.Instance, evaluator);

    private CapabilityEvaluator Evaluator(CapabilitySnapshotV1 snapshot)
    {
        var runtime = new LocalCapabilityRuntimeStateProvider(_catalog, TimeProvider.System);
        runtime.Publish(snapshot);
        return new CapabilityEvaluator(_catalog, runtime);
    }

    private CapabilitySnapshotV1 Snapshot(
        long revision = 1,
        bool moduleEnabled = true,
        string? disabledFeature = null)
    {
        var now = new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero);
        return new CapabilitySnapshotV1(
            CapabilityContractV1.ContractVersion,
            CapabilityContractV1.CatalogVersion,
            1,
            revision,
            now,
            now,
            _catalog.Modules.Select(module => new CapabilityModuleStateV1(
                module.Code,
                module.Code != CapabilityModuleCodes.StoreManagement || moduleEnabled)).ToArray(),
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
