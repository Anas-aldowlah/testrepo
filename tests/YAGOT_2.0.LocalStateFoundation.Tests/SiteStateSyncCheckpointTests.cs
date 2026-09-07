using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateSyncCheckpointTests
{
    private static readonly DateTimeOffset AttemptedAt =
        new(2026, 9, 7, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AttemptUpsert_CreatesDiagnosticsOnlyRowWithoutBusinessState()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using (var context = database.CreateContext())
        {
            var store = new SiteStateSyncDiagnosticsStore(context);
            await store.RecordAttemptAsync(1, AttemptedAt, CancellationToken.None);
        }

        await using var verification = database.CreateContext();
        var checkpoint = await verification.SiteStateSyncCheckpoints.SingleAsync();
        Assert.Equal(AttemptedAt, checkpoint.LastAttemptAtUtc);
        Assert.Null(checkpoint.LastSuccessAtUtc);
        Assert.Null(checkpoint.LastObservedRemoteRevision);
        Assert.Equal(0, checkpoint.ConsecutiveFailures);
        Assert.Null(checkpoint.LastFailureCode);
        Assert.Empty(await verification.LocalSiteStateSnapshots.ToListAsync());
        Assert.Empty(await verification.SiteStateEventReceipts.ToListAsync());
    }

    [Fact]
    public async Task FailuresIncrementAndSuccessResetsCheckpoint()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using (var context = database.CreateContext())
        {
            var store = new SiteStateSyncDiagnosticsStore(context);
            await store.RecordFailureAsync(
                1,
                AttemptedAt,
                AttemptedAt.AddSeconds(1),
                null,
                "network_error",
                CancellationToken.None);
            await store.RecordFailureAsync(
                1,
                AttemptedAt.AddMinutes(1),
                AttemptedAt.AddMinutes(1).AddSeconds(1),
                4,
                "remote_revision_older",
                CancellationToken.None);
            await store.RecordSuccessAsync(
                1,
                AttemptedAt.AddMinutes(2),
                AttemptedAt.AddMinutes(2).AddSeconds(1),
                5,
                CancellationToken.None);
        }

        await using var verification = database.CreateContext();
        var checkpoint = await verification.SiteStateSyncCheckpoints.SingleAsync();
        Assert.Equal(AttemptedAt.AddMinutes(2), checkpoint.LastAttemptAtUtc);
        Assert.Equal(
            AttemptedAt.AddMinutes(2).AddSeconds(1),
            checkpoint.LastSuccessAtUtc);
        Assert.Equal(5, checkpoint.LastObservedRemoteRevision);
        Assert.Equal(0, checkpoint.ConsecutiveFailures);
        Assert.Null(checkpoint.LastFailureCode);
    }

    [Fact]
    public async Task ConcurrentFailures_AreAtomicallyCounted()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var tasks = Enumerable.Range(0, 8).Select(async index =>
        {
            await using var context = database.CreateContext();
            var store = new SiteStateSyncDiagnosticsStore(context);
            await store.RecordFailureAsync(
                1,
                AttemptedAt,
                AttemptedAt.AddSeconds(index + 1),
                null,
                "network_error",
                CancellationToken.None);
        });

        await Task.WhenAll(tasks);

        await using var verification = database.CreateContext();
        var checkpoint = await verification.SiteStateSyncCheckpoints.SingleAsync();
        Assert.Equal(8, checkpoint.ConsecutiveFailures);
        Assert.Equal("network_error", checkpoint.LastFailureCode);
    }

    [Fact]
    public async Task ConcurrentSuccessAndFailure_ProduceAnAtomicSerializableCheckpoint()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var success = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var store = new SiteStateSyncDiagnosticsStore(context);
            await release.Task;
            await store.RecordSuccessAsync(
                1,
                AttemptedAt.AddMinutes(1),
                AttemptedAt.AddMinutes(1).AddSeconds(2),
                9,
                CancellationToken.None);
        });
        var failure = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var store = new SiteStateSyncDiagnosticsStore(context);
            await release.Task;
            await store.RecordFailureAsync(
                1,
                AttemptedAt.AddMinutes(1),
                AttemptedAt.AddMinutes(1).AddSeconds(3),
                8,
                "remote_revision_older",
                CancellationToken.None);
        });

        release.TrySetResult();
        await Task.WhenAll(success, failure);

        await using var verification = database.CreateContext();
        var checkpoint = await verification.SiteStateSyncCheckpoints.SingleAsync();
        Assert.Equal(1, checkpoint.SiteId);
        Assert.Equal(AttemptedAt.AddMinutes(1), checkpoint.LastAttemptAtUtc);
        Assert.Equal(
            AttemptedAt.AddMinutes(1).AddSeconds(2),
            checkpoint.LastSuccessAtUtc);
        Assert.Equal(
            AttemptedAt.AddMinutes(1).AddSeconds(3),
            checkpoint.UpdatedAtUtc);

        if (checkpoint.ConsecutiveFailures == 0)
        {
            Assert.Null(checkpoint.LastFailureCode);
            Assert.Equal(9, checkpoint.LastObservedRemoteRevision);
        }
        else
        {
            Assert.Equal(1, checkpoint.ConsecutiveFailures);
            Assert.Equal("remote_revision_older", checkpoint.LastFailureCode);
            Assert.Equal(8, checkpoint.LastObservedRemoteRevision);
        }

        Assert.Empty(await verification.LocalSiteStateSnapshots.ToListAsync());
        Assert.Empty(await verification.SiteStateEventReceipts.ToListAsync());
    }

    [Fact]
    public async Task Reconciliation_UsesDurableApplyCheckpointAndNoReceipt()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var provider = database.CreateRetryEnabledServiceProvider(
            TimeProvider.System);
        await using var applyScope = provider.CreateAsyncScope();
        await using var diagnosticsContext = database.CreateContext();
        var snapshot = SiteStateContractTests.ValidSnapshot() with
        {
            Revision = 2
        };
        var service = new SiteStateReconciliationService(
            new SuccessfulSnapshotClient(snapshot),
            new SiteStateSnapshotV1JsonParser(),
            applyScope.ServiceProvider.GetRequiredService<ISiteStateApplyService>(),
            new SiteStateSyncDiagnosticsStore(diagnosticsContext),
            new NoDelayPolicy(),
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

        var result = await service.ReconcileAsync(
            SiteStateReconciliationReason.Startup,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(SiteStateApplyOutcome.Applied, result.ApplyOutcome);
        await using var verification = database.CreateContext();
        Assert.Equal(
            2,
            (await verification.LocalSiteStateSnapshots.SingleAsync()).Revision);
        Assert.Empty(await verification.SiteStateEventReceipts.ToListAsync());
        var checkpoint = await verification.SiteStateSyncCheckpoints.SingleAsync();
        Assert.Equal(2, checkpoint.LastObservedRemoteRevision);
        Assert.Equal(0, checkpoint.ConsecutiveFailures);
        Assert.NotNull(checkpoint.LastSuccessAtUtc);
    }

    private sealed class SuccessfulSnapshotClient(
        SiteStateSnapshotV1 snapshot) : IControlPanelSnapshotClient
    {
        public Task<ControlPanelSnapshotClientResult> GetSnapshotAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(ControlPanelSnapshotClientResult.Success(
                JsonSerializer.SerializeToUtf8Bytes(
                    snapshot,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    }

    private sealed class NoDelayPolicy : ISiteStateReconciliationPolicy
    {
        public TimeSpan GetStartupDelay() => TimeSpan.Zero;
        public TimeSpan GetSuccessfulCycleDelay() => TimeSpan.Zero;
        public TimeSpan GetFailedCycleDelay(int consecutiveFailures) =>
            TimeSpan.Zero;
        public TimeSpan? GetInlineRetryDelay(
            int nextAttempt,
            TimeSpan? retryAfter) => TimeSpan.Zero;
    }
}
