namespace YAGOT_2._0.Core.Capabilities;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiresCapabilityAttribute(string featureCode) : Attribute
{
    public string FeatureCode { get; } = featureCode;
}
