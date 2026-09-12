namespace YAGOT_2._0.Core.Capabilities;

/// <summary>
/// Supplies local capability state. A future synchronized source can replace this implementation
/// without coupling consumers to Control Panel or persistence details.
/// </summary>
public interface ICapabilityStateProvider
{
    bool IsModuleEnabled(string moduleCode);
    bool IsFeatureEnabled(string featureCode);
}

/// <summary>
/// Phase 2 state: every catalog entry is locally enabled. Implementation status and the
/// core-required rule remain separate evaluator concerns.
/// </summary>
public sealed class CurrentApplicationCapabilityStateProvider(ICapabilityCatalog catalog) : ICapabilityStateProvider
{
    public bool IsModuleEnabled(string moduleCode) => catalog.TryGetModule(moduleCode, out _);
    public bool IsFeatureEnabled(string featureCode) => catalog.TryGetFeature(featureCode, out _);
}
