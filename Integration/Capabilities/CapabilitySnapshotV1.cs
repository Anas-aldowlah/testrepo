using YAGOT_2._0.Core.Capabilities;

namespace YAGOT_2._0.Integration.Capabilities;

public sealed record CapabilityModuleStateV1(string Code, bool Enabled);
public sealed record CapabilityFeatureStateV1(string Code, string ModuleCode, bool Enabled);

public sealed record CapabilitySnapshotV1(
    int ContractVersion,
    string CatalogVersion,
    int SiteId,
    long Revision,
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset EffectiveAtUtc,
    IReadOnlyList<CapabilityModuleStateV1> Modules,
    IReadOnlyList<CapabilityFeatureStateV1> Features)
{
    public void Validate(ICapabilityCatalog catalog, int expectedSiteId)
    {
        if (ContractVersion != CapabilityContractV1.ContractVersion)
            throw new CapabilityContractValidationException($"ContractVersion must be {CapabilityContractV1.ContractVersion}.");
        if (!string.Equals(CatalogVersion, CapabilityContractV1.CatalogVersion, StringComparison.Ordinal))
            throw new CapabilityContractValidationException($"CatalogVersion must be '{CapabilityContractV1.CatalogVersion}'.");
        if (SiteId < 1 || SiteId != expectedSiteId) throw new CapabilityContractValidationException("SiteId is invalid for this deployment.");
        if (Revision < 1) throw new CapabilityContractValidationException("Revision must be at least 1.");
        if (GeneratedAtUtc.Offset != TimeSpan.Zero || EffectiveAtUtc.Offset != TimeSpan.Zero)
            throw new CapabilityContractValidationException("Capability timestamps must be UTC.");
        if (Modules is null || Features is null) throw new CapabilityContractValidationException("Modules and Features are required.");

        var modules = Unique(Modules, x => x.Code, "module");
        var features = Unique(Features, x => x.Code, "feature");
        if (modules.Count != catalog.Modules.Count) throw new CapabilityContractValidationException("The complete module catalog is required.");
        if (features.Count != catalog.Features.Count) throw new CapabilityContractValidationException("The complete feature catalog is required.");

        foreach (var expected in catalog.Modules)
        {
            if (!modules.TryGetValue(expected.Code, out var state)) throw new CapabilityContractValidationException($"Missing module '{expected.Code}'.");
            if (expected.IsCore && !state.Enabled) throw new CapabilityContractValidationException("Core module cannot be disabled.");
        }
        foreach (var expected in catalog.Features)
        {
            if (!features.TryGetValue(expected.Code, out var state)) throw new CapabilityContractValidationException($"Missing feature '{expected.Code}'.");
            if (!string.Equals(state.ModuleCode, expected.ModuleCode, StringComparison.Ordinal))
                throw new CapabilityContractValidationException($"Feature '{expected.Code}' has invalid module ownership.");
            if (expected.IsCore && !state.Enabled) throw new CapabilityContractValidationException("Core features cannot be disabled.");
        }
    }

    private static Dictionary<string, T> Unique<T>(IEnumerable<T> values, Func<T, string> code, string kind)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var key = code(value);
            if (string.IsNullOrWhiteSpace(key) || !result.TryAdd(key, value))
                throw new CapabilityContractValidationException($"Duplicate or invalid {kind} code '{key}'.");
        }
        return result;
    }
}

public sealed class CapabilityContractValidationException(string message) : Exception(message);
