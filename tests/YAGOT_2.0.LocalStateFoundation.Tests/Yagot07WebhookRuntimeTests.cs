using System.Net;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Integration.SiteState;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class Yagot07WebhookRuntimeTests
{
    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task Missing_IsRepairedBySignedWebhook_AndNextRequestUsesCommittedState()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);

        using (var before = await host.RequestAsync(SiteAccessSurface.Storefront, json: true))
        {
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                before, HttpStatusCode.ServiceUnavailable, "site_state_missing");
        }
        Assert.Equal(0, host.ControlPanel.CallCount);

        var delivery = Guid.NewGuid();
        using (var webhook = await host.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Online), delivery))
        {
            Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
            Assert.Contains("\"code\":\"applied\"", await webhook.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        var durable = await host.ReadDatabaseAsync();
        Assert.Equal(1, durable.Snapshot!.Revision);
        Assert.Equal(SiteStateContractV1.Online, durable.Snapshot.Mode);
        Assert.Equal(1, durable.ReceiptCount);

        host.ResetActions();
        using var after = await host.RequestAsync(SiteAccessSurface.Storefront);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal(1, host.ActionCount);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task ObservableModeTransitions_InvalidateBeforeThirtySecondTtl()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Development));

        using (var development = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
        {
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                development, HttpStatusCode.ServiceUnavailable, "site_development");
        }
        var readsAfterPrime = host.SnapshotQueries.Count;

        using (var onlineWebhook = await host.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(2, SiteStateContractV1.Online)))
        {
            Assert.Equal(HttpStatusCode.OK, onlineWebhook.StatusCode);
        }
        host.ResetActions();
        using (var online = await host.RequestAsync(SiteAccessSurface.Storefront, role: "Customer"))
        {
            Assert.Equal(HttpStatusCode.OK, online.StatusCode);
            Assert.Equal(1, host.ActionCount);
        }
        Assert.True(host.SnapshotQueries.Count > readsAfterPrime);

        using (var offlineWebhook = await host.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(3, SiteStateContractV1.Offline)))
        {
            Assert.Equal(HttpStatusCode.OK, offlineWebhook.StatusCode);
        }
        using (var offline = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
        {
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                offline, HttpStatusCode.ServiceUnavailable, "site_offline");
        }

        var durable = await host.ReadDatabaseAsync();
        Assert.Equal(3, durable.Snapshot!.Revision);
        Assert.Equal(2, durable.ReceiptCount);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task WebhookRevisionAndDeliveryDecisions_PreserveAuthoritativeRuntime()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        var appliedId = Guid.NewGuid();
        var applied = Yagot07EndToEndHost.Snapshot(5, SiteStateContractV1.Online, siteName: "Authoritative");

        using (var response = await host.SendWebhookAsync(applied, appliedId))
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var response = await host.RequestAsync(SiteAccessSurface.Storefront))
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reads = host.SnapshotQueries.Count;

        using (var duplicate = await host.SendWebhookAsync(applied, appliedId))
            Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        using (var stale = await host.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(4, SiteStateContractV1.Offline)))
            Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        using (var equal = await host.SendWebhookAsync(applied, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, equal.StatusCode);
        using (var conflict = await host.SendWebhookAsync(
                   applied with { Mode = SiteStateContractV1.Offline }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using (var collision = await host.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(9, SiteStateContractV1.Offline), appliedId))
            Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);

        host.ResetActions();
        using var finalRequest = await host.RequestAsync(SiteAccessSurface.Storefront);
        Assert.Equal(HttpStatusCode.OK, finalRequest.StatusCode);
        Assert.Equal(1, host.ActionCount);
        var durable = await host.ReadDatabaseAsync();
        Assert.Equal(5, durable.Snapshot!.Revision);
        Assert.Equal("Authoritative", durable.Snapshot.SiteName);
        Assert.Equal(4, durable.ReceiptCount);
        Assert.True(host.SnapshotQueries.Count >= reads);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }
}
