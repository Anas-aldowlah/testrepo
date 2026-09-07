using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateReconciliationPersistenceTests
{
    [Fact]
    public async Task FailedPull_WithNoSnapshot_PreservesExplicitMissingAndNoReceipt()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();

        var result = await ReconcileFailureAsync(database);

        Assert.False(result.Succeeded);
        await using var verification = database.CreateContext();
        var read = await new LocalSiteStateReader(verification).ReadAsync();
        Assert.Equal(LocalSiteStateReadStatus.Missing, read.Status);
        Assert.Null(read.Snapshot);
        Assert.Empty(await verification.LocalSiteStateSnapshots.ToListAsync());
        Assert.Empty(await verification.SiteStateEventReceipts.ToListAsync());
        Assert.Equal(
            1,
            (await verification.SiteStateSyncCheckpoints.SingleAsync())
                .ConsecutiveFailures);
    }

    [Fact]
    public async Task FailedPull_WithExistingSnapshot_PreservesExactDurableState()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var existing = SiteStateContractTests.ValidSnapshot(
            revision: 7,
            mode: SiteStateContractV1.Development,
            siteName: "Durable before outage");
        await using (var provider = database.CreateRetryEnabledServiceProvider(
                         TimeProvider.System))
        await using (var scope = provider.CreateAsyncScope())
        {
            var applied = await scope.ServiceProvider
                .GetRequiredService<ISiteStateApplyService>()
                .ApplyAsync(existing, delivery: null);
            Assert.Equal(SiteStateApplyOutcome.Applied, applied.Outcome);
        }

        byte[] beforeBytes;
        await using (var beforeContext = database.CreateContext())
        {
            beforeBytes = Serialize(
                (await beforeContext.LocalSiteStateSnapshots
                    .AsNoTracking()
                    .SingleAsync()).ToContract());
        }

        var result = await ReconcileFailureAsync(database);

        Assert.False(result.Succeeded);
        await using var verification = database.CreateContext();
        var stored = (await verification.LocalSiteStateSnapshots
            .AsNoTracking()
            .SingleAsync()).ToContract();
        Assert.True(existing.LogicallyEquals(stored));
        Assert.Equal(7, stored.Revision);
        Assert.Equal(beforeBytes, Serialize(stored));
        Assert.Empty(await verification.SiteStateEventReceipts.ToListAsync());
    }

    private static async Task<SiteStateReconciliationResult> ReconcileFailureAsync(
        PostgreSqlTestDatabase database)
    {
        await using var provider = database.CreateRetryEnabledServiceProvider(
            TimeProvider.System);
        await using var scope = provider.CreateAsyncScope();
        await using var diagnosticsContext = database.CreateContext();
        var service = new SiteStateReconciliationService(
            new FailedSnapshotClient(),
            new SiteStateSnapshotV1JsonParser(),
            scope.ServiceProvider.GetRequiredService<ISiteStateApplyService>(),
            new SiteStateSyncDiagnosticsStore(diagnosticsContext),
            new NoRetryPolicy(),
            Options.Create(new SiteStateReconciliationOptions
            {
                SiteId = 1,
                ControlPanelBaseUrl = "https://control-panel.test/",
                SnapshotApiKey = "snapshot-test-key",
                ReconciliationIntervalMinutes = 30,
                SnapshotHttpTimeoutSeconds = 10
            }),
            TimeProvider.System,
            NullLogger<SiteStateReconciliationService>.Instance);

        return await service.ReconcileAsync(
            SiteStateReconciliationReason.Startup,
            CancellationToken.None);
    }

    private static byte[] Serialize(SiteStateSnapshotV1 snapshot) =>
        JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private sealed class FailedSnapshotClient : IControlPanelSnapshotClient
    {
        public Task<ControlPanelSnapshotClientResult> GetSnapshotAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(ControlPanelSnapshotClientResult.Failed(
                ControlPanelSnapshotFailure.Network));
    }

    private sealed class NoRetryPolicy : ISiteStateReconciliationPolicy
    {
        public TimeSpan GetStartupDelay() => TimeSpan.Zero;
        public TimeSpan GetSuccessfulCycleDelay() => TimeSpan.FromMinutes(30);
        public TimeSpan GetFailedCycleDelay(int consecutiveFailures) =>
            TimeSpan.FromMinutes(30);
        public TimeSpan? GetInlineRetryDelay(
            int nextAttempt,
            TimeSpan? retryAfter) => null;
    }
}
