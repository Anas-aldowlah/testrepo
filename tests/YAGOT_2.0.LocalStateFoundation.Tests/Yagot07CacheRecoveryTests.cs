using System.Net;
using Microsoft.Extensions.Time.Testing;
using YAGOT_2._0.Integration.SiteState;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class Yagot07CacheRecoveryTests
{
    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task HttpReadRacingWebhookApplyConvergesOnCommittedRuntimeState()
    {
        var barrier = new Yagot07ReadBarrier();
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(
            database,
            new Yagot07HostOptions(ReadBarrier: barrier));
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Development));

        var delayedRead = barrier.DelayNextRead();
        var requestA = host.RequestAsync(
            SiteAccessSurface.Storefront, role: "Customer", json: true);
        await delayedRead.Entered.WaitAsync(TimeSpan.FromSeconds(5));

        using (var update = await host.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(2, SiteStateContractV1.Online)))
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        delayedRead.Release();

        using (var responseA = await requestA)
            Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        using (var responseB = await host.RequestAsync(SiteAccessSurface.Storefront, role: "Customer"))
            Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
        var durable = await host.ReadDatabaseAsync();
        Assert.Equal(2, durable.Snapshot!.Revision);
        Assert.Equal(SiteStateContractV1.Online, durable.Snapshot.Mode);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task StaleWebhookLeavesAlreadyCachedAuthoritativeStateUntouched()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(2, SiteStateContractV1.Development));
        using (var prime = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                prime, HttpStatusCode.ServiceUnavailable, "site_development");

        using (var stale = await host.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Online)))
        {
            Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
            Assert.Contains("\"code\":\"stale\"", await stale.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }
        var readsAfterIngress = host.SnapshotQueries.Count;
        using (var enforced = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                enforced, HttpStatusCode.ServiceUnavailable, "site_development");
        Assert.Equal(readsAfterIngress, host.SnapshotQueries.Count);
        var durable = await host.ReadDatabaseAsync();
        Assert.Equal(2, durable.Snapshot!.Revision);
        Assert.Equal(SiteStateContractV1.Development, durable.Snapshot.Mode);
        Assert.Equal(1, durable.ReceiptCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task EqualConflictWebhookLeavesAlreadyCachedAuthoritativeStateUntouched()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(4, SiteStateContractV1.Development));
        using (var prime = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                prime, HttpStatusCode.ServiceUnavailable, "site_development");

        using (var conflict = await host.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(4, SiteStateContractV1.Online)))
        {
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            Assert.Contains("\"code\":\"equal_conflict\"",
                await conflict.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        var readsAfterIngress = host.SnapshotQueries.Count;
        using (var enforced = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                enforced, HttpStatusCode.ServiceUnavailable, "site_development");
        Assert.Equal(readsAfterIngress, host.SnapshotQueries.Count);
        var durable = await host.ReadDatabaseAsync();
        Assert.Equal(4, durable.Snapshot!.Revision);
        Assert.Equal(SiteStateContractV1.Development, durable.Snapshot.Mode);
        Assert.Equal(1, durable.ReceiptCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task ConcurrentHttpMissesShareOneDurableLoad()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using (var seedHost = await Yagot07EndToEndHost.CreateAsync(database))
            await seedHost.ApplyAsync(Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Online));
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);

        var tasks = Enumerable.Range(0, 30)
            .Select(_ => host.RequestAsync(SiteAccessSurface.Storefront))
            .ToArray();
        var responses = await Task.WhenAll(tasks);
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Assert.Equal(1, host.SnapshotQueries.Count);
            Assert.Equal(0, host.ControlPanel.CallCount);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task RestartRecoversLazilyFromDurableState()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using (var first = await Yagot07EndToEndHost.CreateAsync(database))
        {
            await first.ApplyAsync(Yagot07EndToEndHost.Snapshot(3, SiteStateContractV1.Offline));
            using var primed = await first.RequestAsync(
                SiteAccessSurface.Storefront, role: "Customer", json: true);
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                primed, HttpStatusCode.ServiceUnavailable, "site_offline");
        }

        await using var restarted = await Yagot07EndToEndHost.CreateAsync(database);
        Assert.Equal(0, restarted.SnapshotQueries.Count);
        using var restored = await restarted.RequestAsync(
            SiteAccessSurface.Storefront, role: "Customer", json: true);
        await Yagot07HttpAssertions.HasJsonCodeAsync(
            restored, HttpStatusCode.ServiceUnavailable, "site_offline");
        Assert.Equal(1, restarted.SnapshotQueries.Count);
        Assert.Equal(0, restarted.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task IndependentProvidersConvergeAtUnchangedThirtySecondFreshness()
    {
        var clock = new FakeTimeProvider(
            DateTimeOffset.FromUnixTimeSeconds(SiteStateWebhookAuthenticationTests.NowUnixSeconds));
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var a = await Yagot07EndToEndHost.CreateAsync(database, clock);
        await using var b = await Yagot07EndToEndHost.CreateAsync(database, clock);
        await a.ApplyAsync(Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Development));

        using (var primeB = await b.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                primeB, HttpStatusCode.ServiceUnavailable, "site_development");
        using (var updateA = await a.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(2, SiteStateContractV1.Online)))
            Assert.Equal(HttpStatusCode.OK, updateA.StatusCode);
        using (var currentA = await a.RequestAsync(SiteAccessSurface.Storefront, role: "Customer"))
            Assert.Equal(HttpStatusCode.OK, currentA.StatusCode);

        clock.Advance(TimeSpan.FromSeconds(29));
        using (var staleB = await b.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                staleB, HttpStatusCode.ServiceUnavailable, "site_development");
        Assert.Equal(1, b.SnapshotQueries.Count);

        clock.Advance(TimeSpan.FromSeconds(1));
        b.ResetActions();
        using var convergedB = await b.RequestAsync(SiteAccessSurface.Storefront, role: "Customer");
        Assert.Equal(HttpStatusCode.OK, convergedB.StatusCode);
        Assert.Equal(1, b.ActionCount);
        Assert.Equal(2, b.SnapshotQueries.Count);
        Assert.Equal(0, a.ControlPanel.CallCount);
        Assert.Equal(0, b.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task EqualWebhookDoesNotRenewCacheTtl()
    {
        var clock = new FakeTimeProvider(
            DateTimeOffset.FromUnixTimeSeconds(SiteStateWebhookAuthenticationTests.NowUnixSeconds));
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database, clock);
        var snapshot = Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Online);
        await host.ApplyAsync(snapshot);
        var readsAfterApply = host.SnapshotQueries.Count;
        using (var prime = await host.RequestAsync(SiteAccessSurface.Storefront))
            Assert.Equal(HttpStatusCode.OK, prime.StatusCode);
        Assert.Equal(readsAfterApply + 1, host.SnapshotQueries.Count);

        clock.Advance(TimeSpan.FromSeconds(29));
        using (var equal = await host.SendWebhookAsync(snapshot))
            Assert.Equal(HttpStatusCode.OK, equal.StatusCode);
        var readsAfterEqualApply = host.SnapshotQueries.Count;
        clock.Advance(TimeSpan.FromSeconds(1));
        using (var refreshed = await host.RequestAsync(SiteAccessSurface.Storefront))
            Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(readsAfterEqualApply + 1, host.SnapshotQueries.Count);
    }
}
