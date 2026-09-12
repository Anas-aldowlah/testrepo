namespace YAGOT_2._0.Core.Capabilities;

public enum CapabilityClassification { Core, Business }
public enum CapabilityImplementationStatus { Core, Implemented, PartiallyImplemented, NotImplemented }

public sealed record CapabilityModule(string Code, string DisplayName, CapabilityClassification Classification,
    CapabilityImplementationStatus ImplementationStatus, IReadOnlyList<string> FeatureCodes)
{
    public bool IsCore => Classification == CapabilityClassification.Core;
}

public sealed record CapabilityFeature(string Code, string ModuleCode, string DisplayName,
    CapabilityClassification Classification, CapabilityImplementationStatus ImplementationStatus,
    bool SupportsIndependentRuntimeDisablement)
{
    public bool IsCore => Classification == CapabilityClassification.Core;
}
