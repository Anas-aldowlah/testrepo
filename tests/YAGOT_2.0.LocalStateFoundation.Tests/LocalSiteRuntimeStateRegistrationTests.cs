using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class LocalSiteRuntimeStateRegistrationTests
{
    [Fact]
    public async Task UnrelatedApplicationCache_IsIsolatedFromLocalReadsAndInvalidation()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddScoped<ILocalSiteStateReader>(_ => new MissingReader());
        services.AddLocalSiteRuntimeState();
        await using var provider = services.BuildServiceProvider();
        var applicationCache = provider.GetRequiredService<IMemoryCache>();
        const string unrelatedKey = "test:unrelated-cache-entry";
        var unrelatedValue = new object();
        applicationCache.Set(unrelatedKey, unrelatedValue);
        var runtime = provider.GetRequiredService<ILocalSiteRuntimeStateProvider>();
        Assert.Equal(LocalSiteStateReadStatus.Missing, (await runtime.ReadAsync()).Status);
        provider.GetRequiredService<ILocalSiteRuntimeStateInvalidator>().InvalidateForCommittedDelivery();
        Assert.Same(unrelatedValue, applicationCache.Get<object>(unrelatedKey));
        Assert.Equal(LocalSiteStateReadStatus.Missing, (await runtime.ReadAsync()).Status);
    }

    [Fact]
    public async Task Registration_HasOneSingleton_NoConcreteWriterBypass_AndNoEagerLoad()
    {
        await using var rig = new RuntimeTestRig();
        Assert.Same(rig.Runtime, rig.Invalidator);
        await using var scope = rig.Services.CreateAsyncScope();
        Assert.Same(rig.Runtime, scope.ServiceProvider.GetRequiredService<ILocalSiteRuntimeStateProvider>());
        Assert.IsType<SiteStateRuntimeAwareApplyService>(scope.ServiceProvider.GetRequiredService<ISiteStateApplyService>());
        Assert.Null(scope.ServiceProvider.GetService<SiteStateApplyService>());
        Assert.Equal(0, rig.Reads);
        await rig.Runtime.ReadAsync();
        Assert.Equal(1, rig.DisposedReaders);
        rig.Clock.Advance(TimeSpan.FromSeconds(30));
        await rig.Runtime.ReadAsync();
        Assert.Equal(2, rig.DisposedReaders);
    }

    [Fact]
    public void CachedContract_HasOnlyContractV1Fields()
    {
        Assert.Equal(new[] { "ContractVersion", "EffectiveAtUtc", "ExpiresAtUtc", "Mode", "OriginalDurationDays",
                "Revision", "SiteId", "SiteName", "SiteUrl", "StartDate" },
            typeof(SiteStateSnapshotV1).GetProperties().Select(p => p.Name).OrderBy(p => p).ToArray());
    }

    private sealed class MissingReader : ILocalSiteStateReader
    {
        public Task<LocalSiteStateReadResult> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(LocalSiteStateReadResult.Missing);
    }
}
