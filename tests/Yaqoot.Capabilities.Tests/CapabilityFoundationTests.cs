using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Core.Capabilities;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class CapabilityFoundationTests
{
    private static readonly IReadOnlyDictionary<string, int> ExpectedFeatureCounts =
        new Dictionary<string, int>
        {
            [CapabilityModuleCodes.Core] = 8,
            [CapabilityModuleCodes.StoreManagement] = 4,
            [CapabilityModuleCodes.Categories] = 6,
            [CapabilityModuleCodes.ProductsInventory] = 8,
            [CapabilityModuleCodes.Offers] = 6,
            [CapabilityModuleCodes.CustomerAccounts] = 5,
            [CapabilityModuleCodes.MarketingOrders] = 6,
            [CapabilityModuleCodes.SalesPos] = 8
        };

    private readonly CapabilityCatalog _catalog = new();

    [Fact]
    public void Catalog_HasExactlyEightModulesAndFiftyOneFeatures()
    {
        Assert.Equal(8, _catalog.Modules.Count);
        Assert.Equal(51, _catalog.Features.Count);
    }

    [Fact]
    public void Catalog_HasExpectedFeatureCountsAndUniqueCodes()
    {
        Assert.Equal(_catalog.Modules.Count, _catalog.Modules.Select(module => module.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(_catalog.Features.Count, _catalog.Features.Select(feature => feature.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.All(_catalog.Modules, module => Assert.Equal(ExpectedFeatureCounts[module.Code], module.FeatureCodes.Count));
    }

    [Fact]
    public void Catalog_AssignsEveryFeatureToExactlyOneModule()
    {
        foreach (var feature in _catalog.Features)
        {
            Assert.Equal(1, _catalog.Modules.Count(module => module.FeatureCodes.Contains(feature.Code, StringComparer.Ordinal)));
            Assert.True(_catalog.TryGetModule(feature.ModuleCode, out var module));
            Assert.Contains(feature.Code, module.FeatureCodes);
        }
    }

    [Fact]
    public void Core_CannotBeCommerciallyDisabled()
    {
        Assert.True(_catalog.TryGetModule(CapabilityModuleCodes.Core, out var core));
        Assert.True(core.IsCore);
        var evaluator = CreateEvaluator([CapabilityModuleCodes.Core], [CapabilityFeatureCodes.CoreAuthentication]);

        Assert.Equal(CapabilityEvaluationReason.CoreRequired, evaluator.EvaluateModule(CapabilityModuleCodes.Core).Reason);
        Assert.True(evaluator.IsModuleEnabled(CapabilityModuleCodes.Core));
        Assert.Equal(CapabilityEvaluationReason.CoreRequired, evaluator.EvaluateFeature(CapabilityFeatureCodes.CoreAuthentication).Reason);
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CoreAuthentication));
    }

    [Fact]
    public void DisabledModule_DisablesItsFeatures()
    {
        var result = CreateEvaluator([CapabilityModuleCodes.Categories]).EvaluateFeature(CapabilityFeatureCodes.CategoryView);
        Assert.False(result.IsEnabled);
        Assert.Equal(CapabilityEvaluationReason.ModuleDisabled, result.Reason);
    }

    [Fact]
    public void EnabledModule_AllowsAnIndividualFeatureToBeDisabled()
    {
        var evaluator = CreateEvaluator(disabledFeatures: [CapabilityFeatureCodes.ProductCreate]);
        Assert.True(evaluator.IsModuleEnabled(CapabilityModuleCodes.ProductsInventory));
        Assert.Equal(CapabilityEvaluationReason.FeatureDisabled, evaluator.EvaluateFeature(CapabilityFeatureCodes.ProductCreate).Reason);
    }

    [Fact]
    public void UnknownCodes_AreHandledSafely()
    {
        var evaluator = CreateEvaluator();
        Assert.Equal(CapabilityEvaluationReason.UnknownModule, evaluator.EvaluateModule("M99_UNKNOWN").Reason);
        Assert.Equal(CapabilityEvaluationReason.UnknownFeature, evaluator.EvaluateFeature("UNKNOWN_FEATURE").Reason);
        Assert.False(evaluator.IsModuleEnabled("M99_UNKNOWN"));
        Assert.False(evaluator.IsFeatureEnabled("UNKNOWN_FEATURE"));
    }

    [Fact]
    public void DefaultState_PreservesCurrentImplementedAvailability()
    {
        var evaluator = new CapabilityEvaluator(_catalog, new CurrentApplicationCapabilityStateProvider(_catalog));
        Assert.All(_catalog.Modules.Where(module => module.ImplementationStatus != CapabilityImplementationStatus.NotImplemented),
            module => Assert.True(evaluator.IsModuleEnabled(module.Code)));
        Assert.All(_catalog.Features.Where(feature => feature.ImplementationStatus != CapabilityImplementationStatus.NotImplemented),
            feature => Assert.True(evaluator.IsFeatureEnabled(feature.Code)));
    }

    [Fact]
    public void Offers_AreDefinedAndImplemented()
    {
        var offers = _catalog.Features.Where(feature => feature.ModuleCode == CapabilityModuleCodes.Offers).ToArray();
        var evaluator = CreateEvaluator();
        Assert.Equal(6, offers.Length);
        Assert.All(offers, feature => Assert.Equal(CapabilityImplementationStatus.Implemented, feature.ImplementationStatus));
        Assert.All(offers, feature => Assert.Equal(CapabilityEvaluationReason.Enabled, evaluator.EvaluateFeature(feature.Code).Reason));
    }

    [Fact]
    public void Metadata_IsConsistentAndMarksRuntimeDisablementAsUnsafe()
    {
        Assert.All(_catalog.Modules, module => Assert.NotEmpty(module.DisplayName));
        Assert.All(_catalog.Features, feature =>
        {
            Assert.NotEmpty(feature.DisplayName);
            Assert.False(feature.SupportsIndependentRuntimeDisablement);
            Assert.True(_catalog.TryGetModule(feature.ModuleCode, out _));
        });
    }

    [Fact]
    public void FoundationRegistration_DoesNotInstallRequestEnforcement()
    {
        var services = new ServiceCollection();
        services.AddCapabilityFoundation();

        Assert.DoesNotContain(services, descriptor => typeof(IFilterMetadata).IsAssignableFrom(descriptor.ServiceType));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IStartupFilter));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.IsGenericType
            && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>));
        Assert.False(typeof(IFilterMetadata).IsAssignableFrom(typeof(RequiresCapabilityAttribute)));
    }

    private CapabilityEvaluator CreateEvaluator(
        IEnumerable<string>? disabledModules = null,
        IEnumerable<string>? disabledFeatures = null) =>
        new(_catalog, new TestCapabilityStateProvider(disabledModules, disabledFeatures));

    private sealed class TestCapabilityStateProvider(
        IEnumerable<string>? disabledModules,
        IEnumerable<string>? disabledFeatures) : ICapabilityStateProvider
    {
        private readonly HashSet<string> _disabledModules = new(disabledModules ?? [], StringComparer.Ordinal);
        private readonly HashSet<string> _disabledFeatures = new(disabledFeatures ?? [], StringComparer.Ordinal);
        public bool IsModuleEnabled(string moduleCode) => !_disabledModules.Contains(moduleCode);
        public bool IsFeatureEnabled(string featureCode) => !_disabledFeatures.Contains(featureCode);
    }
}
