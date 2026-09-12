using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class CapabilitySnapshotTests
{
    private readonly CapabilityCatalog _catalog = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    [Fact] public void Catalog_HasRequiredShape() { Assert.Equal(8, _catalog.Modules.Count); Assert.Equal(51, _catalog.Features.Count); }
    [Fact] public void ValidSnapshot_Parses() { Assert.NotNull(Parse(Create()).Snapshot); }
    [Fact] public void UnknownProperty_IsRejected() { var json = Serialize(Create()).Insert(1, "\"unexpected\":true,"); Assert.Null(Parse(json).Snapshot); }
    [Fact] public void DuplicateProperty_IsRejected() { var json = Serialize(Create()).Insert(1, "\"contractVersion\":1,"); Assert.Null(Parse(json).Snapshot); }
    [Fact] public void MissingProperty_IsRejected() { var node = Node(Create()); node.Remove("features"); Assert.Null(Parse(node.ToJsonString()).Snapshot); }
    [Fact] public void InvalidPropertyType_IsRejected() { var node = Node(Create()); node["revision"] = "one"; Assert.Null(Parse(node.ToJsonString()).Snapshot); }
    [Fact] public void InvalidContractVersion_IsRejected() => Assert.Null(Parse(Create() with { ContractVersion = 2 }).Snapshot);
    [Fact] public void InvalidCatalogVersion_IsRejected() => Assert.Null(Parse(Create() with { CatalogVersion = "other" }).Snapshot);
    [Fact] public void InvalidSiteId_IsRejected() => Assert.Null(Parse(Create() with { SiteId = 2 }).Snapshot);
    [Fact] public void InvalidRevision_IsRejected() => Assert.Null(Parse(Create() with { Revision = 0 }).Snapshot);
    [Fact] public void NonUtcTimestamp_IsRejected() => Assert.Null(Parse(Create() with { EffectiveAtUtc = new DateTimeOffset(2026, 1, 1, 3, 0, 0, TimeSpan.FromHours(3)) }).Snapshot);
    [Fact] public void UnknownModule_IsRejected() { var s = Create(); var modules = s.Modules.ToArray(); modules[1] = modules[1] with { Code = "M99" }; Assert.Null(Parse(s with { Modules = modules }).Snapshot); }
    [Fact] public void UnknownFeature_IsRejected() { var s = Create(); var features = s.Features.ToArray(); features[10] = features[10] with { Code = "UNKNOWN" }; Assert.Null(Parse(s with { Features = features }).Snapshot); }
    [Fact] public void DuplicateModule_IsRejected() { var s = Create(); Assert.Null(Parse(s with { Modules = s.Modules.Append(s.Modules[0]).ToArray() }).Snapshot); }
    [Fact] public void DuplicateFeature_IsRejected() { var s = Create(); Assert.Null(Parse(s with { Features = s.Features.Append(s.Features[0]).ToArray() }).Snapshot); }
    [Fact] public void MissingModule_IsRejected() { var s = Create(); Assert.Null(Parse(s with { Modules = s.Modules.Skip(1).ToArray() }).Snapshot); }
    [Fact] public void MissingFeature_IsRejected() { var s = Create(); Assert.Null(Parse(s with { Features = s.Features.Skip(1).ToArray() }).Snapshot); }
    [Fact] public void WrongFeatureOwnership_IsRejected() { var s = Create(); var f = s.Features.ToArray(); f[10] = f[10] with { ModuleCode = CapabilityModuleCodes.Core }; Assert.Null(Parse(s with { Features = f }).Snapshot); }
    [Fact] public void DisabledCoreModule_IsRejected() { var s = Create(); var m = s.Modules.ToArray(); m[0] = m[0] with { Enabled = false }; Assert.Null(Parse(s with { Modules = m }).Snapshot); }
    [Fact] public void DisabledCoreFeature_IsRejected() { var s = Create(); var f = s.Features.ToArray(); f[0] = f[0] with { Enabled = false }; Assert.Null(Parse(s with { Features = f }).Snapshot); }

    [Fact]
    public void Canonicalization_IsDeterministicAcrossOrdering()
    {
        var a = Create();
        var b = a with { Modules = a.Modules.Reverse().ToArray(), Features = a.Features.Reverse().ToArray() };
        Assert.Equal(CapabilitySnapshotCanonicalizer.ToCanonicalJson(a), CapabilitySnapshotCanonicalizer.ToCanonicalJson(b));
        Assert.Equal(CapabilitySnapshotCanonicalizer.Hash(a), CapabilitySnapshotCanonicalizer.Hash(b));
    }

    [Fact]
    public void DifferentState_HasDifferentHash()
    {
        var a = Create(); var modules = a.Modules.ToArray(); modules[1] = modules[1] with { Enabled = false };
        Assert.NotEqual(CapabilitySnapshotCanonicalizer.Hash(a), CapabilitySnapshotCanonicalizer.Hash(a with { Modules = modules }));
    }

    [Fact] public void FirstRevision_IsApplied() { var h = Hash(Create()); Assert.Equal(CapabilityReceiptDecision.Applied, CapabilityRevisionDecider.Decide(10, h, null, [])); }
    [Fact] public void NewerRevision_IsApplied() { var h = Hash(Create()); Assert.Equal(CapabilityReceiptDecision.Applied, CapabilityRevisionDecider.Decide(11, h, 10, h)); }
    [Fact] public void StaleRevision_IsRejectedAsStale() { var h = Hash(Create()); Assert.Equal(CapabilityReceiptDecision.Stale, CapabilityRevisionDecider.Decide(9, h, 10, h)); }
    [Fact] public void EqualRevisionAndPayload_IsEqual() { var h = Hash(Create()); Assert.Equal(CapabilityReceiptDecision.Equal, CapabilityRevisionDecider.Decide(10, h, 10, h)); }
    [Fact] public void EqualRevisionAndDifferentPayload_IsConflict() { var a = Create(); var b = a with { EffectiveAtUtc = a.EffectiveAtUtc.AddMinutes(1) }; Assert.Equal(CapabilityReceiptDecision.EqualConflict, CapabilityRevisionDecider.Decide(10, Hash(a), 10, Hash(b))); }
    [Fact] public void DeliverySameCanonicalPayload_IsDuplicateCompatible() { var a = Create(); var b = a with { Features = a.Features.Reverse().ToArray() }; Assert.Equal(Hash(a), Hash(b)); }
    [Fact] public void DeliveryDifferentPayload_IsConflictCompatible() { var a = Create(); Assert.NotEqual(Hash(a), Hash(a with { Revision = 2 })); }

    [Fact]
    public void RuntimeWithoutSnapshot_IsPermissive()
    {
        var runtime = Runtime();
        Assert.True(runtime.IsModuleEnabled(CapabilityModuleCodes.Categories));
        Assert.True(runtime.IsFeatureEnabled(CapabilityFeatureCodes.CategoryView));
        Assert.Equal(CapabilityHealthStatus.NoSnapshot, runtime.Health);
    }

    [Fact]
    public void RuntimePublishesImmutableSnapshot()
    {
        var snapshot = Create(); var modules = snapshot.Modules.ToArray(); modules[2] = modules[2] with { Enabled = false };
        var runtime = Runtime(); runtime.Publish(snapshot with { Modules = modules });
        modules[2] = modules[2] with { Enabled = true };
        Assert.False(runtime.IsModuleEnabled(CapabilityModuleCodes.Categories));
        Assert.Equal(1, runtime.Current!.Revision);
    }

    [Fact]
    public void RuntimeRetainsLastSnapshotWhenDegraded()
    {
        var runtime = Runtime(); var snapshot = Create(); runtime.Publish(snapshot); runtime.Report(CapabilityHealthStatus.StorageFailure);
        Assert.Equal(snapshot.Revision, runtime.Current!.Revision);
        Assert.True(runtime.IsFeatureEnabled(CapabilityFeatureCodes.ProductView));
    }

    [Fact]
    public void RuntimeIgnoresStalePublication()
    {
        var runtime = Runtime(); runtime.Publish(Create() with { Revision = 2 }); runtime.Publish(Create() with { Revision = 1 });
        Assert.Equal(2, runtime.Current!.Revision);
    }

    [Fact]
    public void EvaluatorKeepsCoreAndOffersAuthoritative()
    {
        var runtime = Runtime(); var s = Create(); var m = s.Modules.Select(x => x.Code == CapabilityModuleCodes.Offers ? x with { Enabled = true } : x).ToArray(); runtime.Publish(s with { Modules = m });
        var evaluator = new CapabilityEvaluator(_catalog, runtime);
        Assert.Equal(CapabilityEvaluationReason.CoreRequired, evaluator.EvaluateModule(CapabilityModuleCodes.Core).Reason);
        Assert.Equal(CapabilityEvaluationReason.NotImplemented, evaluator.EvaluateModule(CapabilityModuleCodes.Offers).Reason);
    }

    [Fact]
    public void EvaluatorReportsModuleAndFeatureDisablement()
    {
        var s = Create(); var modules = s.Modules.Select(x => x.Code == CapabilityModuleCodes.Categories ? x with { Enabled = false } : x).ToArray();
        var features = s.Features.Select(x => x.Code == CapabilityFeatureCodes.ProductCreate ? x with { Enabled = false } : x).ToArray();
        var runtime = Runtime(); runtime.Publish(s with { Modules = modules, Features = features }); var evaluator = new CapabilityEvaluator(_catalog, runtime);
        Assert.Equal(CapabilityEvaluationReason.ModuleDisabled, evaluator.EvaluateFeature(CapabilityFeatureCodes.CategoryView).Reason);
        Assert.Equal(CapabilityEvaluationReason.FeatureDisabled, evaluator.EvaluateFeature(CapabilityFeatureCodes.ProductCreate).Reason);
        Assert.Equal(CapabilityEvaluationReason.UnknownFeature, evaluator.EvaluateFeature("UNKNOWN").Reason);
    }

    [Fact]
    public void PersistenceModel_UsesDedicatedTablesAndReceiptIndex()
    {
        var options = new DbContextOptionsBuilder<NeondbContext>().UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").Options;
        using var context = new NeondbContext(options);
        Assert.Equal("local_capability_snapshots", context.Model.FindEntityType(typeof(LocalCapabilitySnapshot))!.GetTableName());
        Assert.Equal("capability_event_receipts", context.Model.FindEntityType(typeof(CapabilityEventReceipt))!.GetTableName());
        Assert.Equal("capability_sync_checkpoints", context.Model.FindEntityType(typeof(CapabilitySyncCheckpoint))!.GetTableName());
        Assert.Contains(context.Model.FindEntityType(typeof(CapabilityEventReceipt))!.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(CapabilityEventReceipt.SiteId), nameof(CapabilityEventReceipt.Revision)]));
    }

    [Fact]
    public async Task InvalidApply_IsRejectedBeforeStorageAndDoesNotChangeRuntime()
    {
        var runtime = Runtime();
        var service = ApplyService(runtime);
        var result = await service.ApplyAsync(Create() with { Revision = 0 });
        Assert.Equal(CapabilityApplyOutcome.Rejected, result.Outcome);
        Assert.Null(runtime.Current);
        Assert.Equal(CapabilityHealthStatus.InvalidSnapshot, runtime.Health);
    }

    [Fact]
    public async Task StorageFailure_RetainsLastValidRuntimeSnapshot()
    {
        var runtime = Runtime(); runtime.Publish(Create());
        var result = await ApplyService(runtime).ApplyAsync(Create() with { Revision = 2 });
        Assert.Equal(CapabilityHealthStatus.StorageFailure, result.Health);
        Assert.Equal(1, runtime.Current!.Revision);
    }

    [Fact]
    public void Registration_DoesNotInstallCapabilityEnforcement()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddCapabilityFoundation();
        Assert.DoesNotContain(services, x => typeof(Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata).IsAssignableFrom(x.ServiceType));
        Assert.False(typeof(Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata).IsAssignableFrom(typeof(RequiresCapabilityAttribute)));
    }

    private CapabilitySnapshotV1 Create()
    {
        var at = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
        return new(CapabilityContractV1.ContractVersion, CapabilityContractV1.CatalogVersion, 1, 1, at, at,
            _catalog.Modules.Select(x => new CapabilityModuleStateV1(x.Code, true)).ToArray(),
            _catalog.Features.Select(x => new CapabilityFeatureStateV1(x.Code, x.ModuleCode, true)).ToArray());
    }

    private CapabilitySnapshotParseResult Parse(CapabilitySnapshotV1 value) => Parse(Serialize(value));
    private CapabilitySnapshotParseResult Parse(string value) => new CapabilitySnapshotV1JsonParser(_catalog, Options.Create(new CapabilityIntegrationOptions { SiteId = 1 })).Parse(Encoding.UTF8.GetBytes(value));
    private string Serialize(CapabilitySnapshotV1 value) => JsonSerializer.Serialize(value, _json);
    private JsonObject Node(CapabilitySnapshotV1 value) => JsonNode.Parse(Serialize(value))!.AsObject();
    private static byte[] Hash(CapabilitySnapshotV1 value) => CapabilitySnapshotCanonicalizer.Hash(value);
    private LocalCapabilityRuntimeStateProvider Runtime() => new(_catalog, TimeProvider.System);
    private CapabilityApplyService ApplyService(LocalCapabilityRuntimeStateProvider runtime) => new(
        new ThrowingScopeFactory(), _catalog, Options.Create(new CapabilityIntegrationOptions { SiteId = 1 }),
        runtime, TimeProvider.System, NullLogger<CapabilityApplyService>.Instance);

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("Storage unavailable.");
    }
}
