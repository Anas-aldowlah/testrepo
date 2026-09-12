namespace YAGOT_2._0.Core.Capabilities;

public sealed class CapabilityEvaluator(ICapabilityCatalog catalog, ICapabilityStateProvider stateProvider) : ICapabilityEvaluator
{
    public CapabilityEvaluationResult EvaluateModule(string moduleCode)
    {
        if (string.IsNullOrWhiteSpace(moduleCode) || !catalog.TryGetModule(moduleCode, out var module))
            return new(false, CapabilityEvaluationReason.UnknownModule, moduleCode ?? string.Empty);
        if (module.IsCore) return new(true, CapabilityEvaluationReason.CoreRequired, module.Code, module.Code);
        if (module.ImplementationStatus == CapabilityImplementationStatus.NotImplemented)
            return new(false, CapabilityEvaluationReason.NotImplemented, module.Code, module.Code);
        return stateProvider.IsModuleEnabled(module.Code)
            ? new(true, CapabilityEvaluationReason.Enabled, module.Code, module.Code)
            : new(false, CapabilityEvaluationReason.ModuleDisabled, module.Code, module.Code);
    }

    public CapabilityEvaluationResult EvaluateFeature(string featureCode)
    {
        if (string.IsNullOrWhiteSpace(featureCode) || !catalog.TryGetFeature(featureCode, out var feature))
            return new(false, CapabilityEvaluationReason.UnknownFeature, featureCode ?? string.Empty);
        if (feature.IsCore) return new(true, CapabilityEvaluationReason.CoreRequired, feature.Code, feature.ModuleCode);
        if (feature.ImplementationStatus == CapabilityImplementationStatus.NotImplemented)
            return new(false, CapabilityEvaluationReason.NotImplemented, feature.Code, feature.ModuleCode);
        if (!EvaluateModule(feature.ModuleCode).IsEnabled)
            return new(false, CapabilityEvaluationReason.ModuleDisabled, feature.Code, feature.ModuleCode);
        return stateProvider.IsFeatureEnabled(feature.Code)
            ? new(true, CapabilityEvaluationReason.Enabled, feature.Code, feature.ModuleCode)
            : new(false, CapabilityEvaluationReason.FeatureDisabled, feature.Code, feature.ModuleCode);
    }

    public bool IsModuleEnabled(string moduleCode) => EvaluateModule(moduleCode).IsEnabled;
    public bool IsFeatureEnabled(string featureCode) => EvaluateFeature(featureCode).IsEnabled;
}
