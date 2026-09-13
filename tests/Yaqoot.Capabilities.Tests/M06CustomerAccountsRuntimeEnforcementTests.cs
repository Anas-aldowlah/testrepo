using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Controllers;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class M06CustomerAccountsRuntimeEnforcementTests
{
    private static readonly string[] M06Features =
    [
        CapabilityFeatureCodes.CustomerAccountCreate,
        CapabilityFeatureCodes.CustomerLogin,
        CapabilityFeatureCodes.CustomerGoogleLogin,
        CapabilityFeatureCodes.CustomerProfile,
        CapabilityFeatureCodes.CustomerAccountRecovery
    ];

    private readonly CapabilityCatalog _catalog = new();

    [Fact]
    public void EnabledModule_AllowsEachEnabledM06Feature()
    {
        var evaluator = Evaluator(Snapshot());

        Assert.True(evaluator.IsModuleEnabled(CapabilityModuleCodes.CustomerAccounts));
        Assert.All(M06Features, code => Assert.True(evaluator.IsFeatureEnabled(code)));
    }

    [Fact]
    public void DisabledModule_DisablesAllFiveM06Features()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));

        Assert.False(evaluator.IsModuleEnabled(CapabilityModuleCodes.CustomerAccounts));
        Assert.All(M06Features, code =>
            Assert.Equal(CapabilityEvaluationReason.ModuleDisabled, evaluator.EvaluateFeature(code).Reason));
    }

    [Theory]
    [MemberData(nameof(IndependentFeatureCases))]
    public void DisabledFeature_DoesNotDisableOtherM06Features(string disabledFeature, string enabledFeature)
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: disabledFeature));

        Assert.Equal(CapabilityEvaluationReason.FeatureDisabled, evaluator.EvaluateFeature(disabledFeature).Reason);
        Assert.True(evaluator.IsFeatureEnabled(enabledFeature));
    }

    [Fact]
    public async Task AccountCreationOff_BlocksRegistrationGetAndMutationBoundariesBeforeDependencies()
    {
        var controller = Controller(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CustomerAccountCreate)));

        Assert.IsType<NotFoundResult>(controller.Auth(register: true));
        Assert.IsType<NotFoundResult>(await controller.GoogleLogin(isRegister: true));
        Assert.IsType<NotFoundResult>(controller.GetRegisterState());
        Assert.IsType<NotFoundResult>(await controller.SaveStep2Info(null!));
        Assert.IsType<NotFoundResult>(await controller.CompleteRegistration(null!));
        Assert.IsType<NotFoundResult>(controller.SetRegisterStep(2));
        Assert.IsType<NotFoundResult>(controller.ResetRegistration());
    }

    [Fact]
    public async Task GoogleLoginOff_BlocksChallengeCallbackAndGoogleDependentRegistrationBeforeInfrastructure()
    {
        var controller = Controller(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CustomerGoogleLogin)));

        Assert.IsType<NotFoundResult>(await controller.GoogleLogin());
        Assert.IsType<NotFoundResult>(await controller.GoogleResponse());
        Assert.IsType<NotFoundResult>(controller.GetRegisterState());
        Assert.IsType<NotFoundResult>(await controller.CompleteRegistration(null!));
    }

    [Fact]
    public async Task CustomerLoginOff_BlocksDirectCustomerPostBeforeValidationOrQueries()
    {
        var controller = Controller(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CustomerLogin)));

        Assert.IsType<NotFoundResult>(await controller.Login(new AccountController.LoginModel()));
    }

    [Fact]
    public async Task ProfileOff_BlocksCustomerGetAndPostButDoesNotRemoveAdminProfileBoundary()
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CustomerProfile));
        var customer = Controller(evaluator, Principal("Customer"));
        var admin = Controller(evaluator, Principal("Admin"));

        Assert.IsType<NotFoundResult>(await customer.Profile());
        Assert.IsType<NotFoundResult>(await customer.Profile(new ProfileVM()));
        Assert.IsType<ChallengeResult>(await admin.Profile());
    }

    [Fact]
    public async Task RecoveryOff_BlocksRequestResetAndPasswordMutationBeforeQueriesOrEmail()
    {
        var controller = Controller(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CustomerAccountRecovery)));

        Assert.IsType<NotFoundResult>(controller.RecoveryAccount());
        Assert.IsType<NotFoundResult>(await controller.RecoveryAccount(new RecoveryModel()));
        Assert.IsType<NotFoundResult>(await controller.ResetPassword((string?)null));
        Assert.IsType<NotFoundResult>(await controller.ResetPassword(new ResetPasswordModel()));
    }

    [Fact]
    public async Task DisabledModule_BlocksEveryPureCustomerBoundaryWithoutDisablingLogout()
    {
        var controller = Controller(Evaluator(Snapshot(moduleEnabled: false)), Principal("Customer"));

        Assert.IsType<NotFoundResult>(controller.Auth(register: true));
        Assert.IsType<NotFoundResult>(await controller.Login(new AccountController.LoginModel()));
        Assert.IsType<NotFoundResult>(await controller.GoogleLogin());
        Assert.IsType<NotFoundResult>(await controller.GoogleResponse());
        Assert.IsType<NotFoundResult>(controller.GetRegisterState());
        Assert.IsType<NotFoundResult>(await controller.SaveStep2Info(null!));
        Assert.IsType<NotFoundResult>(await controller.CompleteRegistration(null!));
        Assert.IsType<NotFoundResult>(controller.SetRegisterStep(1));
        Assert.IsType<NotFoundResult>(controller.ResetRegistration());
        Assert.IsType<NotFoundResult>(await controller.Profile());
        Assert.IsType<NotFoundResult>(await controller.Profile(new ProfileVM()));
        Assert.IsType<NotFoundResult>(controller.RecoveryAccount());
        Assert.IsType<NotFoundResult>(await controller.RecoveryAccount(new RecoveryModel()));
        Assert.IsType<NotFoundResult>(await controller.ResetPassword((string?)null));
        Assert.IsType<NotFoundResult>(await controller.ResetPassword(new ResetPasswordModel()));
    }

    [Fact]
    public void M06ModuleOff_DoesNotDisableCoreAdminOrShoppingCapabilities()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));

        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CoreAuthentication));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.StoreDashboard));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CategoryView));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.ProductView));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CartView));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.Checkout));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.PosCashier));
    }

    [Fact]
    public void RazorUi_UsesM06CapabilitiesForCustomerControls()
    {
        var root = FindSolutionRoot();

        AssertFileContains(root, Path.Combine("Views", "Account", "Auth.cshtml"),
            "CustomerLogin", "CustomerGoogleLogin", "CustomerAccountCreate", "CustomerAccountRecovery", "adminSignIn");
        AssertFileContains(root, Path.Combine("Views", "Shared", "_StorefrontHeader.cshtml"),
            "CustomerLogin", "CustomerGoogleLogin", "CustomerAccountCreate", "CustomerProfile");
        AssertFileContains(root, Path.Combine("Views", "Account", "Profile.cshtml"), "CustomerAccountRecovery");
        AssertFileContains(root, Path.Combine("Views", "Orders", "_CheckoutAuthModal.cshtml"),
            "CustomerLogin", "CustomerGoogleLogin", "CustomerAccountCreate");
    }

    [Fact]
    public void AccountController_PreservesAdminPasswordLoginAndDoesNotGateLogout()
    {
        var root = FindSolutionRoot();
        var source = File.ReadAllText(Path.Combine(root, "Controllers", "AccountController.cs"));

        Assert.Contains("userSiteVB?.Role is \"Admin\" or \"Developer\"", source);
        Assert.Contains("if (!isAdministrator && !IsEnabled(CapabilityFeatureCodes.CustomerLogin))", source);
        Assert.DoesNotContain("CustomerLogin))\n        {\n            await HttpContext.SignOutAsync", source);
        Assert.DoesNotContain("CapabilityFeatureCodes", ExtractMethod(source, "public async Task<IActionResult> Logout()", "[HttpGet]"));
    }

    public static TheoryData<string, string> IndependentFeatureCases => new()
    {
        { CapabilityFeatureCodes.CustomerAccountCreate, CapabilityFeatureCodes.CustomerLogin },
        { CapabilityFeatureCodes.CustomerLogin, CapabilityFeatureCodes.CustomerGoogleLogin },
        { CapabilityFeatureCodes.CustomerGoogleLogin, CapabilityFeatureCodes.CustomerProfile },
        { CapabilityFeatureCodes.CustomerProfile, CapabilityFeatureCodes.CustomerAccountRecovery },
        { CapabilityFeatureCodes.CustomerAccountRecovery, CapabilityFeatureCodes.CustomerAccountCreate }
    };

    private AccountController Controller(ICapabilityEvaluator evaluator, ClaimsPrincipal? principal = null)
    {
        var controller = new AccountController(
            null!,
            new ConfigurationBuilder().Build(),
            null!,
            null!,
            null!,
            null!,
            new EphemeralDataProtectionProvider(),
            Options.Create(new PublicUrlOptions { BaseUrl = "https://yaqoot.test" }),
            NullLogger<AccountController>.Instance,
            evaluator);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal ?? new ClaimsPrincipal(new ClaimsIdentity()) }
        };
        return controller;
    }

    private static ClaimsPrincipal Principal(string role) => new(
        new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));

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
                module.Code != CapabilityModuleCodes.CustomerAccounts || moduleEnabled)).ToArray(),
            _catalog.Features.Select(feature => new CapabilityFeatureStateV1(
                feature.Code,
                feature.ModuleCode,
                !string.Equals(feature.Code, disabledFeature, StringComparison.Ordinal))).ToArray());
    }

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
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
