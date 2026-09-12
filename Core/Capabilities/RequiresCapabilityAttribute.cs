namespace YAGOT_2._0.Core.Capabilities;

/// <summary>
/// Associates endpoint metadata with a stable feature code. Phase 2 deliberately provides no
/// filter or middleware that enforces this marker.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiresCapabilityAttribute(string featureCode) : Attribute
{
    public string FeatureCode { get; } = featureCode;
}
