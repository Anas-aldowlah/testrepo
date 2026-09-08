using System.Net;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using YAGOT_2._0.Integration.SiteState;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class Yagot07FailureRecoveryTests
{
    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task ControlPanelTimeoutPreservesLocalStateUntilLaterRealClientRecovery()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Development));
        using (var prime = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                prime, HttpStatusCode.ServiceUnavailable, "site_development");

        host.ControlPanel.SetSnapshot(Yagot07EndToEndHost.Snapshot(2, SiteStateContractV1.Online));
        host.ControlPanel.DelayNextResponse();
        var timedOut = await host.ReconcileAsync();

        Assert.False(timedOut.Succeeded);
        Assert.Equal(SiteStateReconciliationFailureCodes.Timeout, timedOut.FailureCode);
        var afterTimeout = await host.ReadDatabaseAsync();
        Assert.Equal(1, afterTimeout.Snapshot!.Revision);
        Assert.Equal(SiteStateContractV1.Development, afterTimeout.Snapshot.Mode);
        var callsBeforeBrowser = host.ControlPanel.CallCount;
        using (var stillLocal = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                stillLocal, HttpStatusCode.ServiceUnavailable, "site_development");
        Assert.Equal(callsBeforeBrowser, host.ControlPanel.CallCount);

        var recovered = await host.ReconcileAsync();
        Assert.True(recovered.Succeeded);
        host.ResetActions();
        using var online = await host.RequestAsync(SiteAccessSurface.Storefront, role: "Customer");
        Assert.Equal(HttpStatusCode.OK, online.StatusCode);
        Assert.Equal(1, host.ActionCount);
        Assert.Equal(callsBeforeBrowser + 1, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task WarmCacheContinuesToEnforceWithoutDurableReadUntilTtlExpires()
    {
        var clock = new FakeTimeProvider(
            DateTimeOffset.FromUnixTimeSeconds(SiteStateWebhookAuthenticationTests.NowUnixSeconds));
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database, clock);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Online));
        using (var prime = await host.RequestAsync(SiteAccessSurface.Storefront))
            Assert.Equal(HttpStatusCode.OK, prime.StatusCode);
        var readsAfterWarm = host.SnapshotQueries.Count;

        host.SnapshotQueries.FailReads = true;
        host.ResetActions();
        using (var warm = await host.RequestAsync(SiteAccessSurface.Storefront, role: "Customer"))
        {
            Assert.Equal(HttpStatusCode.OK, warm.StatusCode);
            Assert.Equal(1, host.ActionCount);
        }
        Assert.Equal(readsAfterWarm, host.SnapshotQueries.Count);
        Assert.Equal(0, host.ControlPanel.CallCount);

        clock.Advance(TimeSpan.FromSeconds(30));
        using var expired = await host.RequestAsync(
            SiteAccessSurface.Storefront, role: "Customer", json: true);
        await Yagot07HttpAssertions.HasJsonCodeAsync(
            expired, HttpStatusCode.ServiceUnavailable, "site_state_storage_unavailable");
        Assert.True(host.SnapshotQueries.Count > readsAfterWarm);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task FailedWebhookApplyKeepsCachedMissingUntilSuccessfulRepair()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);

        using (var missing = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                missing, HttpStatusCode.ServiceUnavailable, "site_state_missing");

        host.FailingSave.Enabled = true;
        using (var failed = await host.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Online)))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        var afterFailure = await host.ReadDatabaseAsync();
        Assert.Null(afterFailure.Snapshot);
        Assert.Equal(0, afterFailure.ReceiptCount);
        var readsAfterRollback = host.SnapshotQueries.Count;

        using (var stillMissing = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                stillMissing, HttpStatusCode.ServiceUnavailable, "site_state_missing");
        Assert.Equal(readsAfterRollback, host.SnapshotQueries.Count);

        host.FailingSave.Enabled = false;
        using (var repaired = await host.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Online)))
            Assert.Equal(HttpStatusCode.OK, repaired.StatusCode);
        host.ResetActions();
        using var online = await host.RequestAsync(SiteAccessSurface.Storefront, role: "Customer");
        Assert.Equal(HttpStatusCode.OK, online.StatusCode);
        Assert.Equal(1, host.ActionCount);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task PostCommitAmbiguityRetriesAsDuplicateAndRepairsCachedMissing()
    {
        var postCommit = new Yagot07FailOnceAfterCommitInterceptor();
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(
            database,
            new Yagot07HostOptions(),
            postCommit);
        using (var missing = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                missing, HttpStatusCode.ServiceUnavailable, "site_state_missing");

        var delivery = Guid.NewGuid();
        var snapshot = Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Online);
        postCommit.Enabled = true;
        using (var ambiguous = await host.SendWebhookAsync(snapshot, delivery))
        {
            Assert.Equal(HttpStatusCode.OK, ambiguous.StatusCode);
            Assert.Contains("\"code\":\"duplicate_delivery\"",
                await ambiguous.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        Assert.Equal(1, postCommit.FailureCount);
        var durable = await host.ReadDatabaseAsync();
        Assert.Equal(1, durable.Snapshot!.Revision);
        Assert.Equal(1, durable.ReceiptCount);

        host.ResetActions();
        using (var online = await host.RequestAsync(SiteAccessSurface.Storefront, role: "Customer"))
        {
            Assert.Equal(HttpStatusCode.OK, online.StatusCode);
            Assert.Equal(1, host.ActionCount);
        }
        using (var duplicate = await host.SendWebhookAsync(snapshot, delivery))
            Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var afterRetry = await host.ReadDatabaseAsync();
        Assert.Equal(1, afterRetry.Snapshot!.Revision);
        Assert.Equal(1, afterRetry.ReceiptCount);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task WebhookProtocolFailuresNeverMutateStateOrBecomeMaintenanceRedirects()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        var valid = Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Online);

        using (var badHmac = await host.SendWebhookAsync(
                   valid, secret: "wrong-secret-that-is-still-long-enough"))
            Assert.Equal(HttpStatusCode.Unauthorized, badHmac.StatusCode);

        using (var malformedRequest = host.SignedWebhookRequest(
                   Encoding.UTF8.GetBytes("{malformed"), Guid.NewGuid(),
                   SiteStateWebhookAuthenticationTests.TestSecret))
        using (var malformed = await host.Client.SendAsync(malformedRequest))
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);

        using (var invalid = await host.SendWebhookAsync(valid with { Mode = "Invalid" }))
            Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        using (var wrongSite = await host.SendWebhookAsync(valid with { SiteId = 2 }))
            Assert.Equal(HttpStatusCode.Forbidden, wrongSite.StatusCode);

        var durable = await host.ReadDatabaseAsync();
        Assert.Null(durable.Snapshot);
        Assert.Equal(0, durable.ReceiptCount);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task TemporaryWebhookDatabaseFailureRollsBackThenLaterDeliveryRepairsState()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Online));
        using (var prime = await host.RequestAsync(SiteAccessSurface.Storefront))
            Assert.Equal(HttpStatusCode.OK, prime.StatusCode);

        host.FailingSave.Enabled = true;
        using (var failed = await host.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(2, SiteStateContractV1.Offline)))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
            Assert.DoesNotContain("YAGOT07_DB_FAILURE_MARKER",
                await failed.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        var afterFailure = await host.ReadDatabaseAsync();
        Assert.Equal(1, afterFailure.Snapshot!.Revision);
        Assert.Equal(SiteStateContractV1.Online, afterFailure.Snapshot.Mode);
        Assert.Equal(0, afterFailure.ReceiptCount);
        host.ResetActions();
        using (var stillOnline = await host.RequestAsync(SiteAccessSurface.Storefront))
        {
            Assert.Equal(HttpStatusCode.OK, stillOnline.StatusCode);
            Assert.Equal(1, host.ActionCount);
        }

        host.FailingSave.Enabled = false;
        using (var repaired = await host.SendWebhookAsync(
                   Yagot07EndToEndHost.Snapshot(2, SiteStateContractV1.Offline)))
            Assert.Equal(HttpStatusCode.OK, repaired.StatusCode);
        using var enforced = await host.RequestAsync(
            SiteAccessSurface.Storefront, role: "Customer", json: true);
        await Yagot07HttpAssertions.HasJsonCodeAsync(
            enforced, HttpStatusCode.ServiceUnavailable, "site_offline");
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task ReconciliationFailuresPreserveState_AndLaterSuccessRecovers()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Development));
        host.ControlPanel.SetFailure(HttpStatusCode.ServiceUnavailable);

        var unavailable = await host.ReconcileAsync();
        Assert.False(unavailable.Succeeded);
        Assert.Equal(SiteStateReconciliationFailureCodes.ServerError, unavailable.FailureCode);
        host.ControlPanel.SetInvalidBody();
        var invalid = await host.ReconcileAsync();
        Assert.False(invalid.Succeeded);
        Assert.Equal(SiteStateReconciliationFailureCodes.MalformedJson, invalid.FailureCode);

        var callsBeforeBrowser = host.ControlPanel.CallCount;
        using (var unchanged = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                unchanged, HttpStatusCode.ServiceUnavailable, "site_development");
        Assert.Equal(callsBeforeBrowser, host.ControlPanel.CallCount);

        host.ControlPanel.SetSnapshot(Yagot07EndToEndHost.Snapshot(2, SiteStateContractV1.Online));
        var recovered = await host.ReconcileAsync();
        Assert.True(recovered.Succeeded);
        host.ResetActions();
        using var online = await host.RequestAsync(SiteAccessSurface.Storefront, role: "Customer");
        Assert.Equal(HttpStatusCode.OK, online.StatusCode);
        Assert.Equal(1, host.ActionCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task ControlPanelOutageNeverBecomesRequestTimeFallback()
    {
        var clock = new FakeTimeProvider(
            DateTimeOffset.FromUnixTimeSeconds(SiteStateWebhookAuthenticationTests.NowUnixSeconds));
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database, clock);
        await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(1, SiteStateContractV1.Online));
        host.ControlPanel.SetFailure(HttpStatusCode.ServiceUnavailable);

        using (var warm = await host.RequestAsync(SiteAccessSurface.Storefront))
            Assert.Equal(HttpStatusCode.OK, warm.StatusCode);
        Assert.Equal(0, host.ControlPanel.CallCount);

        clock.Advance(TimeSpan.FromSeconds(30));
        host.SnapshotQueries.FailReads = true;
        using (var cold = await host.RequestAsync(
                   SiteAccessSurface.Storefront, role: "Customer", json: true))
            await Yagot07HttpAssertions.HasJsonCodeAsync(
                cold, HttpStatusCode.ServiceUnavailable, "site_state_storage_unavailable");
        Assert.Equal(0, host.ControlPanel.CallCount);

        host.SnapshotQueries.FailReads = false;
        clock.Advance(TimeSpan.FromSeconds(1));
        host.ResetActions();
        using var recovered = await host.RequestAsync(SiteAccessSurface.Storefront);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(1, host.ActionCount);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }

    [Fact]
    [Trait("Suite", "YAGOT07PostgreSql")]
    public async Task WebhookProtocolStaysReachableInEveryCachedModeAndStorageFailure()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await Yagot07EndToEndHost.CreateAsync(database);
        long revision = 0;
        foreach (var mode in new[]
                 {
                     SiteStateContractV1.Online,
                     SiteStateContractV1.Development,
                     SiteStateContractV1.Offline
                 })
        {
            await host.ApplyAsync(Yagot07EndToEndHost.Snapshot(++revision, mode));
            using var prime = await host.RequestAsync(
                SiteAccessSurface.Storefront, role: "Developer");
            Assert.Equal(HttpStatusCode.OK, prime.StatusCode);
            using var webhook = await host.SendWebhookAsync(
                Yagot07EndToEndHost.Snapshot(++revision, mode));
            Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        }

        host.FailingSave.Enabled = true;
        using var storageFailure = await host.SendWebhookAsync(
            Yagot07EndToEndHost.Snapshot(++revision, SiteStateContractV1.Online));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, storageFailure.StatusCode);
        Assert.Null(storageFailure.Headers.Location);
        Assert.Equal(0, host.ControlPanel.CallCount);
    }
}
