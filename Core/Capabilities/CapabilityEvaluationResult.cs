namespace YAGOT_2._0.Core.Capabilities;

public enum CapabilityEvaluationReason
{
    Enabled,
    ModuleDisabled,
    FeatureDisabled,
    CoreRequired,
    NotImplemented,
    MissingPermission,
    OperationallyUnavailable,
    UnknownModule,
    UnknownFeature
}

public sealed record CapabilityEvaluationResult(
    bool IsEnabled,
    CapabilityEvaluationReason Reason,
    string CapabilityCode,
    string? ModuleCode = null);
