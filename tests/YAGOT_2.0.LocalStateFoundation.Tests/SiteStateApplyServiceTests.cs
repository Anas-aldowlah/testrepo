using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateApplyServiceTests
{
    private static readonly DateTimeOffset RecordedAt =
        new(2026, 9, 7, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reader_ReturnsExplicitMissingBeforeFirstApply()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var context = database.CreateContext();

        var result = await new LocalSiteStateReader(context).ReadAsync();

        Assert.Equal(LocalSiteStateReadStatus.Missing, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task InitialReconciliationApply_PersistsSnapshotWithoutReceipt()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var result = await WithServiceAsync(
            database,
            service => service.ApplyAsync(SiteStateContractTests.ValidSnapshot()));
        Assert.Equal(SiteStateApplyOutcome.Applied, result.Outcome);

        await using var verification = database.CreateContext();
        var read = await new LocalSiteStateReader(verification).ReadAsync();
        Assert.Equal(LocalSiteStateReadStatus.Found, read.Status);
        Assert.Equal(1, read.Snapshot!.Revision);
        Assert.Empty(await verification.SiteStateEventReceipts.ToListAsync());
    }

    [Fact]
    public async Task NewerRevision_ReplacesCompleteSnapshotAndRecordsReceipt()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var delivery = Delivery(2);
        var newer = SiteStateContractTests.ValidSnapshot(
            revision: 2,
            mode: SiteStateContractV1.Development,
            siteName: "Updated");

        var result = await WithServiceAsync(database, async service =>
        {
            await service.ApplyAsync(SiteStateContractTests.ValidSnapshot());
            return await service.ApplyAsync(newer, delivery);
        });

        Assert.Equal(SiteStateApplyOutcome.Applied, result.Outcome);
        await using var context = database.CreateContext();
        Assert.True(newer.LogicallyEquals(
            (await context.LocalSiteStateSnapshots.SingleAsync()).ToContract()));
        var receipt = await context.SiteStateEventReceipts.SingleAsync();
        Assert.Equal("Applied", receipt.Decision);
        Assert.Equal(RecordedAt, receipt.RecordedAtUtc);
    }

    [Fact]
    public async Task EqualIdenticalRevision_IsNoOpAndRecordsEqual()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var snapshot = SiteStateContractTests.ValidSnapshot();
        var result = await WithServiceAsync(database, async service =>
        {
            await service.ApplyAsync(snapshot);
            return await service.ApplyAsync(snapshot, Delivery(3));
        });

        Assert.Equal(SiteStateApplyOutcome.Equal, result.Outcome);
        await using var context = database.CreateContext();
        Assert.Equal("Equal", (await context.SiteStateEventReceipts.SingleAsync()).Decision);
    }

    [Fact]
    public async Task EqualConflictingRevision_NeverMutatesSnapshot()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var original = SiteStateContractTests.ValidSnapshot(siteName: "Original");
        var result = await WithServiceAsync(database, async service =>
        {
            await service.ApplyAsync(original);
            return await service.ApplyAsync(
                original with { SiteName = "Conflict" },
                Delivery(4));
        });

        Assert.Equal(SiteStateApplyOutcome.EqualConflict, result.Outcome);
        await using var context = database.CreateContext();
        Assert.Equal("Original", (await context.LocalSiteStateSnapshots.SingleAsync()).SiteName);
        Assert.Equal("EqualConflict", (await context.SiteStateEventReceipts.SingleAsync()).Decision);
    }

    [Fact]
    public async Task StaleRevision_NeverOverwritesNewerSnapshot()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var result = await WithServiceAsync(database, async service =>
        {
            await service.ApplyAsync(
                SiteStateContractTests.ValidSnapshot(revision: 5, siteName: "Newest"));
            return await service.ApplyAsync(
                SiteStateContractTests.ValidSnapshot(revision: 4, siteName: "Stale"),
                Delivery(5));
        });

        Assert.Equal(SiteStateApplyOutcome.Stale, result.Outcome);
        await using var context = database.CreateContext();
        var stored = await context.LocalSiteStateSnapshots.SingleAsync();
        Assert.Equal(5, stored.Revision);
        Assert.Equal("Newest", stored.SiteName);
        Assert.Equal("Stale", (await context.SiteStateEventReceipts.SingleAsync()).Decision);
    }

    [Fact]
    public async Task DuplicateDeliveryWithSameHash_IsIdempotent()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var delivery = Delivery(6);
        var result = await WithServiceAsync(database, async service =>
        {
            await service.ApplyAsync(SiteStateContractTests.ValidSnapshot(), delivery);
            return await service.ApplyAsync(
                SiteStateContractTests.ValidSnapshot(revision: 9, siteName: "Must not apply"),
                delivery);
        });

        Assert.Equal(SiteStateApplyOutcome.DuplicateDelivery, result.Outcome);
        Assert.Equal(SiteStateReceiptDecision.Applied, result.OriginalDeliveryDecision);
        await using var context = database.CreateContext();
        Assert.Equal(1, (await context.LocalSiteStateSnapshots.SingleAsync()).Revision);
        Assert.Equal(1, await context.SiteStateEventReceipts.CountAsync());
    }

    [Fact]
    public async Task DuplicateDeliveryWithAlteredHash_IsConflictWithoutMutation()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var delivery = Delivery(7);
        var altered = delivery with { PayloadSha256 = SiteStateContractTests.Hash(8) };
        var result = await WithServiceAsync(database, async service =>
        {
            await service.ApplyAsync(SiteStateContractTests.ValidSnapshot(), delivery);
            return await service.ApplyAsync(
                SiteStateContractTests.ValidSnapshot(revision: 9, siteName: "Must not apply"),
                altered);
        });

        Assert.Equal(SiteStateApplyOutcome.DeliveryIdPayloadConflict, result.Outcome);
        await using var context = database.CreateContext();
        Assert.Equal(1, (await context.LocalSiteStateSnapshots.SingleAsync()).Revision);
        Assert.Equal(1, await context.SiteStateEventReceipts.CountAsync());
    }

    [Fact]
    public async Task FailureAfterDatabaseWrite_RollsBackSnapshotAndReceipt()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WithServiceAsync(
                database,
                service => service.ApplyAsync(
                    SiteStateContractTests.ValidSnapshot(),
                    Delivery(9)),
                new ThrowAfterSaveInterceptor()));

        await using var verification = database.CreateContext();
        Assert.Empty(await verification.LocalSiteStateSnapshots.ToListAsync());
        Assert.Empty(await verification.SiteStateEventReceipts.ToListAsync());
    }

    [Fact]
    public async Task SnapshotAndReceipt_PersistAfterContextRecreation()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var delivery = Delivery(10);
        await WithServiceAsync(
            database,
            service => service.ApplyAsync(
                SiteStateContractTests.ValidSnapshot(revision: 3),
                delivery));

        await using var reader = database.CreateContext();
        Assert.Equal(3, (await new LocalSiteStateReader(reader).ReadAsync()).Snapshot!.Revision);
        Assert.Equal(delivery.DeliveryId,
            (await reader.SiteStateEventReceipts.SingleAsync()).DeliveryId);
    }

    [Fact]
    public async Task ConcurrentApplies_ConvergeToHighestRevision()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var lower = WithServiceAsync(
            database,
            service => service.ApplyAsync(
                SiteStateContractTests.ValidSnapshot(revision: 2, siteName: "Lower"),
                Delivery(11)));
        var higher = WithServiceAsync(
            database,
            service => service.ApplyAsync(
                SiteStateContractTests.ValidSnapshot(revision: 3, siteName: "Higher"),
                Delivery(12)));

        await Task.WhenAll(lower, higher);

        await using var verification = database.CreateContext();
        var stored = await verification.LocalSiteStateSnapshots.SingleAsync();
        Assert.Equal(3, stored.Revision);
        Assert.Equal("Higher", stored.SiteName);
        Assert.Equal(2, await verification.SiteStateEventReceipts.CountAsync());
    }

    [Fact]
    public async Task TransientFailureBeforeDurableWrite_RetriesWithOneSnapshotAndReceipt()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var interceptor = new FailOnceBeforeAdvisoryLockCompletesInterceptor();

        var result = await WithServiceAsync(
            database,
            service => service.ApplyAsync(
                SiteStateContractTests.ValidSnapshot(),
                Delivery(13)),
            interceptor);

        Assert.Equal(SiteStateApplyOutcome.Applied, result.Outcome);
        Assert.Equal(2, interceptor.AdvisoryLockAttempts);
        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.LocalSiteStateSnapshots.CountAsync());
        Assert.Equal(1, await verification.SiteStateEventReceipts.CountAsync());
    }

    [Fact]
    public async Task TransientFailureAfterEntitiesTracked_RetriesWithCleanChangeTracker()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var interceptor = new FailOnceWhileSavingTrackedEntitiesInterceptor();

        var result = await WithServiceAsync(
            database,
            service => service.ApplyAsync(
                SiteStateContractTests.ValidSnapshot(),
                Delivery(14)),
            interceptor);

        Assert.Equal(SiteStateApplyOutcome.Applied, result.Outcome);
        Assert.True(interceptor.SawTrackedSnapshotAndReceipt);
        Assert.Equal(2, interceptor.SaveAttempts);
        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.LocalSiteStateSnapshots.CountAsync());
        Assert.Equal(1, await verification.SiteStateEventReceipts.CountAsync());
    }

    [Fact]
    public async Task TransientFailureAfterCommit_RecoversAsDurableDuplicateDelivery()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var delivery = Delivery(15);
        var interceptor = new FailOnceAfterCommitInterceptor();

        var retried = await WithServiceAsync(
            database,
            service => service.ApplyAsync(
                SiteStateContractTests.ValidSnapshot(),
                delivery),
            interceptor);
        var duplicate = await WithServiceAsync(
            database,
            service => service.ApplyAsync(
                SiteStateContractTests.ValidSnapshot(revision: 9, siteName: "Must not apply"),
                delivery));

        Assert.Equal(SiteStateApplyOutcome.DuplicateDelivery, retried.Outcome);
        Assert.Equal(SiteStateReceiptDecision.Applied, retried.OriginalDeliveryDecision);
        Assert.Equal(SiteStateApplyOutcome.DuplicateDelivery, duplicate.Outcome);
        await using var verification = database.CreateContext();
        Assert.Equal(1, (await verification.LocalSiteStateSnapshots.SingleAsync()).Revision);
        Assert.Equal(1, await verification.SiteStateEventReceipts.CountAsync());
    }

    [Fact]
    public async Task RetriedStaleDelivery_NeverRollsBackNewerRevision()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await WithServiceAsync(
            database,
            service => service.ApplyAsync(
                SiteStateContractTests.ValidSnapshot(revision: 5, siteName: "Newest")));
        var interceptor = new FailOnceWhileSavingTrackedEntitiesInterceptor();

        var result = await WithServiceAsync(
            database,
            service => service.ApplyAsync(
                SiteStateContractTests.ValidSnapshot(revision: 4, siteName: "Stale"),
                Delivery(16)),
            interceptor);

        Assert.Equal(SiteStateApplyOutcome.Stale, result.Outcome);
        Assert.Equal(2, interceptor.SaveAttempts);
        await using var verification = database.CreateContext();
        var stored = await verification.LocalSiteStateSnapshots.SingleAsync();
        Assert.Equal(5, stored.Revision);
        Assert.Equal("Newest", stored.SiteName);
        Assert.Equal(1, await verification.SiteStateEventReceipts.CountAsync());
    }

    [Fact]
    public async Task CancellationDuringTransientFailure_StopsBeforeRetry()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var interceptor = new CancelThenFailTransientlyInterceptor(cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            WithServiceAsync(
                database,
                service => service.ApplyAsync(
                    SiteStateContractTests.ValidSnapshot(),
                    Delivery(17),
                    cancellation.Token),
                interceptor));

        Assert.Equal(1, interceptor.AdvisoryLockAttempts);
        await using var verification = database.CreateContext();
        Assert.Empty(await verification.LocalSiteStateSnapshots.ToListAsync());
        Assert.Empty(await verification.SiteStateEventReceipts.ToListAsync());
    }

    private static SiteStateDeliveryContext Delivery(byte value) =>
        new(Guid.NewGuid(), SiteStateContractTests.Hash(value));

    private static async Task<TResult> WithServiceAsync<TResult>(
        PostgreSqlTestDatabase database,
        Func<ISiteStateApplyService, Task<TResult>> operation,
        params IInterceptor[] interceptors)
    {
        await using var provider = database.CreateRetryEnabledServiceProvider(
            new FixedTimeProvider(RecordedAt),
            interceptors);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ISiteStateApplyService>();
        return await operation(service);
    }

    private static NpgsqlException TransientFailure() =>
        new("Simulated transient PostgreSQL failure.", new TimeoutException());

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class ThrowAfterSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<SiteStateEventReceipt>().Any() == true)
            {
                throw new InvalidOperationException("Simulated failure before transaction commit.");
            }

            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class FailOnceBeforeAdvisoryLockCompletesInterceptor
        : DbCommandInterceptor
    {
        private int _advisoryLockAttempts;

        public int AdvisoryLockAttempts => Volatile.Read(ref _advisoryLockAttempts);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            FailFirstAdvisoryLock(command);
            return base.NonQueryExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            FailFirstAdvisoryLock(command);
            return base.ReaderExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }

        private void FailFirstAdvisoryLock(DbCommand command)
        {
            if (!command.CommandText.Contains(
                    "pg_advisory_xact_lock",
                    StringComparison.Ordinal))
            {
                return;
            }

            if (Interlocked.Increment(ref _advisoryLockAttempts) == 1)
            {
                throw TransientFailure();
            }
        }
    }

    private sealed class FailOnceWhileSavingTrackedEntitiesInterceptor
        : SaveChangesInterceptor
    {
        private int _saveAttempts;

        public int SaveAttempts => Volatile.Read(ref _saveAttempts);
        public bool SawTrackedSnapshotAndReceipt { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context is not null)
            {
                SawTrackedSnapshotAndReceipt |=
                    context.ChangeTracker.Entries<LocalSiteStateSnapshot>().Any() &&
                    context.ChangeTracker.Entries<SiteStateEventReceipt>().Any();
            }

            if (Interlocked.Increment(ref _saveAttempts) == 1)
            {
                throw TransientFailure();
            }

            return base.SavingChangesAsync(
                eventData,
                result,
                cancellationToken);
        }
    }

    private sealed class FailOnceAfterCommitInterceptor : DbTransactionInterceptor
    {
        private int _commitCount;

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _commitCount) == 1)
            {
                throw TransientFailure();
            }

            return base.TransactionCommittedAsync(
                transaction,
                eventData,
                cancellationToken);
        }
    }

    private sealed class CancelThenFailTransientlyInterceptor : DbCommandInterceptor
    {
        private readonly CancellationTokenSource _cancellation;
        private int _advisoryLockAttempts;

        public CancelThenFailTransientlyInterceptor(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        public int AdvisoryLockAttempts => Volatile.Read(ref _advisoryLockAttempts);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CancelFirstAdvisoryLock(command);
            return base.NonQueryExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CancelFirstAdvisoryLock(command);
            return base.ReaderExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }

        private void CancelFirstAdvisoryLock(DbCommand command)
        {
            if (!command.CommandText.Contains(
                    "pg_advisory_xact_lock",
                    StringComparison.Ordinal))
            {
                return;
            }

            if (Interlocked.Increment(ref _advisoryLockAttempts) == 1)
            {
                _cancellation.Cancel();
                throw TransientFailure();
            }
        }
    }
}
