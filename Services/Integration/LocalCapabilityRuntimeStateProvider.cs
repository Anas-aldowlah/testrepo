using System.Collections.Immutable;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Integration.Capabilities;

namespace YAGOT_2._0.Services.Integration;

public sealed record CapabilityRuntimeSnapshot(
    int SiteId, long Revision,
    ImmutableDictionary<string, bool> Modules,
    ImmutableDictionary<string, bool> Features,
    DateTimeOffset PublishedAtUtc);

public interface ILocalCapabilityRuntimeState
{
    CapabilityRuntimeSnapshot? Current { get; }
    CapabilityHealthStatus Health { get; }
}

public interface ICapabilityRuntimePublisher
{
    void Publish(CapabilitySnapshotV1 snapshot);
    void Report(CapabilityHealthStatus health);
}

public sealed class LocalCapabilityRuntimeStateProvider(ICapabilityCatalog catalog, TimeProvider timeProvider)
    : ICapabilityStateProvider, ILocalCapabilityRuntimeState, ICapabilityRuntimePublisher
{
    private CapabilityRuntimeSnapshot? _current;
    private int _health = (int)CapabilityHealthStatus.NoSnapshot;
    public CapabilityRuntimeSnapshot? Current => Volatile.Read(ref _current);
    public CapabilityHealthStatus Health => (CapabilityHealthStatus)Volatile.Read(ref _health);

    public bool IsModuleEnabled(string moduleCode)
    {
        var current = Current;
        return current is null ? catalog.TryGetModule(moduleCode, out _) : current.Modules.TryGetValue(moduleCode, out var enabled) && enabled;
    }

    public bool IsFeatureEnabled(string featureCode)
    {
        var current = Current;
        return current is null ? catalog.TryGetFeature(featureCode, out _) : current.Features.TryGetValue(featureCode, out var enabled) && enabled;
    }

    public void Publish(CapabilitySnapshotV1 snapshot)
    {
        var immutable = new CapabilityRuntimeSnapshot(snapshot.SiteId, snapshot.Revision,
            snapshot.Modules.ToImmutableDictionary(x => x.Code, x => x.Enabled, StringComparer.Ordinal),
            snapshot.Features.ToImmutableDictionary(x => x.Code, x => x.Enabled, StringComparer.Ordinal),
            timeProvider.GetUtcNow());
        while (true)
        {
            var current = Current;
            if (current is not null && snapshot.Revision < current.Revision) return;
            if (ReferenceEquals(Interlocked.CompareExchange(ref _current, immutable, current), current)) break;
        }
        Volatile.Write(ref _health, (int)CapabilityHealthStatus.Healthy);
    }

    public void Report(CapabilityHealthStatus health) => Volatile.Write(ref _health, (int)health);
}
