namespace YAGOT_2._0.Core.Capabilities;

public interface ICapabilityEvaluator
{
    CapabilityEvaluationResult EvaluateModule(string moduleCode);
    CapabilityEvaluationResult EvaluateFeature(string featureCode);
    bool IsModuleEnabled(string moduleCode);
    bool IsFeatureEnabled(string featureCode);
}
