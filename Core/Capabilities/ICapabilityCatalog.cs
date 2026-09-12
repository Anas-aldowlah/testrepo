namespace YAGOT_2._0.Core.Capabilities;

public interface ICapabilityCatalog
{
    IReadOnlyList<CapabilityModule> Modules { get; }
    IReadOnlyList<CapabilityFeature> Features { get; }
    bool TryGetModule(string moduleCode, out CapabilityModule module);
    bool TryGetFeature(string featureCode, out CapabilityFeature feature);
}
