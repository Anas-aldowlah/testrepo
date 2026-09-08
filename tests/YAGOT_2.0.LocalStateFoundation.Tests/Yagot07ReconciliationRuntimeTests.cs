using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using YAGOT_2._0.Integration.SiteState;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class Yagot07ReconciliationRuntimeTests
{
    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task BackgroundService_ReconcilesWithoutBrowserTrafficOrHttpContext()
    {
        var clock = new FakeTimeProvider(
            DateTimeOffset.FromUnixTimeSeconds(SiteStateWebhookAuthenticationTests.NowUnixSeconds));
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(
            database,
            new Yagot07HostOptions(clock, TimeSpan.FromSeconds(1)));
        host.ControlPanel.SetSnapshot(Yagot07EndToEndHost.Snapshot(7, SiteStateContractV1.Online));

        using (var beforeStart = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                beforeStart, HttpStatusCode.ServiceUnavailable, "site_state_missing");
        Assert.Equal(0, host.ControlPanel.CallCount);

        var delayed = host.ControlPanel.DelayNextResponse();
        await host.BackgroundPolicy.StartupDelayRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(1));
        await delayed.Entered.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, host.ControlPanel.CallCount);
        Assert.Equal(HttpMethod.Get, host.ControlPanel.LastRequest!.Method);
        Assert.Equal("/api/v1/sites/1/snapshot", host.ControlPanel.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal(Yagot07EndToEndHost.SnapshotApiKey,
            host.ControlPanel.LastRequest.Headers.GetValues(ControlPanelSnapshotClient.ApiKeyHeaderName).Single());

        using (var whileBackgroundIsBlocked = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                whileBackgroundIsBlocked, HttpStatusCode.ServiceUnavailable, "site_state_missing");
        Assert.Equal(1, host.ControlPanel.CallCount);

        delayed.Release();
        var trigger = await host.ReconciliationRuns.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(SiteStateReconciliationReason.Startup, host.ReconciliationRuns.LastReason);
        Assert.Equal(SiteStateReconciliationTriggerStatus.Completed, trigger.Status);
        Assert.True(trigger.Result!.Succeeded);

        var durable = await host.ReadDatabaseAsync();
        Assert.Equal(7, durable.Snapshot!.Revision);
        Assert.Equal(SiteStateContractV1.Online, durable.Snapshot.Mode);
        Assert.Equal(0, durable.ReceiptCount);

        host.ResetActions();
        using var repaired = await host.RequestAsync(SiteAccessSurface.Storefront, role: "Customer");
        Assert.Equal(HttpStatusCode.OK, repaired.StatusCode);
        Assert.Equal(1, host.ActionCount);
        Assert.Equal(1, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task InitialRecovery_UsesRealClientAndPersistsAllFieldsWithoutReceipt()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        var expected = Yagot07EndToEndHost.Snapshot(
            7, SiteStateContractV1.Offline, siteName: "Recovered Site") with
        {
            SiteUrl = "https://recovered.example/"
        };
        host.ControlPanel.SetSnapshot(expected);

        var result = await host.ReconcileAsync(SiteStateReconciliationReason.Startup);

        Assert.True(result.Succeeded);
        Assert.Equal(SiteStateApplyOutcome.Applied, result.ApplyOutcome);
        Assert.Equal(1, host.ControlPanel.CallCount);
        Assert.Equal(HttpMethod.Get, host.ControlPanel.LastRequest!.Method);
        Assert.Equal("/api/v1/sites/1/snapshot", host.ControlPanel.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal(Yagot07EndToEndHost.SnapshotApiKey,
            host.ControlPanel.LastRequest.Headers.GetValues(ControlPanelSnapshotClient.ApiKeyHeaderName).Single());

        var durable = await host.ReadDatabaseAsync();
        Assert.NotNull(durable.Snapshot);
        Assert.Equal(expected.ContractVersion, durable.Snapshot!.ContractVersion);
        Assert.Equal(expected.SiteId, durable.Snapshot.SiteId);
        Assert.Equal(expected.Mode, durable.Snapshot.Mode);
        Assert.Equal(expected.Revision, durable.Snapshot.Revision);
        Assert.Equal(expected.EffectiveAtUtc, durable.Snapshot.EffectiveAtUtc);
        Assert.Equal(expected.ExpiresAtUtc, durable.Snapshot.ExpiresAtUtc);
        Assert.Equal(expected.SiteName, durable.Snapshot.SiteName);
        Assert.Equal(expected.SiteUrl, durable.Snapshot.SiteUrl);
        Assert.Equal(expected.StartDate, durable.Snapshot.StartDate);
        Assert.Equal(expected.OriginalDurationDays, durable.Snapshot.OriginalDurationDays);
        Assert.Equal(0, durable.ReceiptCount);
        Assert.Equal(7, durable.Checkpoint!.LastObservedRemoteRevision);
        Assert.Equal(0, durable.Checkpoint.ConsecutiveFailures);

        var callsBeforeBrowser = host.ControlPanel.CallCount;
        using var request = await host.RequestAsync(
            SiteAccessSurface.Storefront, role: "Customer", json: true);
        await Yagot07HttpAssertions.HasJsonCodeAsync(
            request, HttpStatusCode.ServiceUnavailable, "site_offline");
        Assert.Equal(callsBeforeBrowser, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task MissedWebhook_IsRepairedOnlyByReconciliation_AndBrowserDoesNotPull()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Development));
        using (var before = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                before, HttpStatusCode.ServiceUnavailable, "site_development");

        host.ControlPanel.SetSnapshot(Yagot07EndToEndHost.Snapshot(2, SiteStateContractV1.Online));
        var callsBeforeRepair = host.ControlPanel.CallCount;
        using (var stillOld = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                stillOld, HttpStatusCode.ServiceUnavailable, "site_development");
        Assert.Equal(callsBeforeRepair, host.ControlPanel.CallCount);

        var reconciliation = await host.ReconcileAsync();
        Assert.True(reconciliation.Succeeded);
        Assert.Equal(callsBeforeRepair + 1, host.ControlPanel.CallCount);
        host.ResetActions();
        using var repaired = await host.RequestAsync(SiteAccessSurface.Storefront, role: "Customer");
        Assert.Equal(HttpStatusCode.OK, repaired.StatusCode);
        Assert.Equal(1, host.ActionCount);
        Assert.Equal(callsBeforeRepair + 1, host.ControlPanel.CallCount);
        var durable = await host.ReadDatabaseAsync();
        Assert.Equal(2, durable.Snapshot!.Revision);
        Assert.Equal(0, durable.ReceiptCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task CachedMissing_IsInvalidatedByRealClientReconciliationBeforeTtl()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);

        using (var missing = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                missing, HttpStatusCode.ServiceUnavailable, "site_state_missing");
        var readsAfterMissing = host.SnapshotQueries.Count;
        Assert.Equal(1, readsAfterMissing);

        host.ControlPanel.SetSnapshot(Yagot07EndToEndHost.Snapshot(8, SiteStateContractV1.Online));
        var reconciliation = await host.ReconcileAsync();
        Assert.True(reconciliation.Succeeded);
        var readsAfterApply = host.SnapshotQueries.Count;

        host.ResetActions();
        using var repaired = await host.RequestAsync(SiteAccessSurface.Storefront, role: "Customer");
        Assert.Equal(HttpStatusCode.OK, repaired.StatusCode);
        Assert.Equal(1, host.ActionCount);
        Assert.Equal(readsAfterApply + 1, host.SnapshotQueries.Count);
        Assert.Equal(1, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task DiagnosticsFailureAfterCommittedApply_PreservesAuthoritativeStateAndHttpEnforcement()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        host.ControlPanel.SetSnapshot(Yagot07EndToEndHost.Snapshot(9, SiteStateContractV1.Offline));
        host.FailingDiagnostics.Enabled = true;

        var reconciliation = await host.ReconcileAsync();

        Assert.True(reconciliation.Succeeded);
        Assert.Equal(SiteStateApplyOutcome.Applied, reconciliation.ApplyOutcome);
        Assert.Equal(1, host.FailingDiagnostics.FailureCount);
        var durable = await host.ReadDatabaseAsync();
        Assert.Equal(9, durable.Snapshot!.Revision);
        Assert.Equal(SiteStateContractV1.Offline, durable.Snapshot.Mode);
        Assert.Equal(0, durable.ReceiptCount);

        using var enforced = await host.RequestAsync(
            SiteAccessSurface.Storefront, role: "Customer", json: true);
        await Yagot07HttpAssertions.HasJsonCodeAsync(
            enforced, HttpStatusCode.ServiceUnavailable, "site_offline");
        Assert.Equal(1, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task DelayedOlderReconciliation_CannotDowngradeNewerWebhook()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Online));
        host.ControlPanel.SetSnapshot(Yagot07EndToEndHost.Snapshot(2, SiteStateContractV1.Development));
        var delayed = host.ControlPanel.DelayNextResponse();

        var reconciliationTask = host.ReconcileAsync();
        await delayed.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        using (var webhook = await host.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(3, SiteStateContractV1.Offline)))
            Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        delayed.Release();
        var reconciliation = await reconciliationTask;

        Assert.False(reconciliation.Succeeded);
        Assert.Equal(SiteStateReconciliationFailureCodes.RemoteRevisionOlder,
            reconciliation.FailureCode);
        var durable = await host.ReadDatabaseAsync();
        Assert.Equal(3, durable.Snapshot!.Revision);
        Assert.Equal(SiteStateContractV1.Offline, durable.Snapshot.Mode);
        using var request = await host.RequestAsync(
            SiteAccessSurface.Storefront, role: "Customer", json: true);
        await Yagot07HttpAssertions.HasJsonCodeAsync(
            request, HttpStatusCode.ServiceUnavailable, "site_offline");
    }
}
