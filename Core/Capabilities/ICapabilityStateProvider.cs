namespace YAGOT_2._0.Core.Capabilities;

public interface ICapabilityStateProvider
{
    bool IsModuleEnabled(string moduleCode);
    bool IsFeatureEnabled(string featureCode);
}

public sealed class CurrentApplicationCapabilityStateProvider(ICapabilityCatalog catalog) : ICapabilityStateProvider
{
    public bool IsModuleEnabled(string moduleCode) => catalog.TryGetModule(moduleCode, out _);
    public bool IsFeatureEnabled(string featureCode) => catalog.TryGetFeature(featureCode, out _);
}
