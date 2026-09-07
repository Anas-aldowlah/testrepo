using System.Data.Common;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class LocalSiteRuntimeStatePostgreSqlTests
{
    [Fact]
    public async Task SignedWebhook_InvalidatesMissingAndOlderState_WithoutLegacyCalls()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var host = await WebhookTestHost.CreateWithDatabaseAsync(database);
        var runtime = host.Services.GetRequiredService<ILocalSiteRuntimeStateProvider>();
        Assert.Equal(LocalSiteStateReadStatus.Missing, (await runtime.ReadAsync()).Status);
        using var first = await host.SendSignedAsync(SiteStateWebhookEndpointTests.ValidBody(revision: 1), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(1, (await runtime.ReadAsync()).Snapshot!.Revision);
        using var second = await host.SendSignedAsync(SiteStateWebhookEndpointTests.ValidBody(revision: 2), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, (await runtime.ReadAsync()).Snapshot!.Revision);
        Assert.Equal(0, host.LegacyStatusCallCount);
    }

    [Fact]
    public async Task Reconciliation_UsesDecoratedApply_AndReadNeverTriggersPull()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var services = database.CreateRetryEnabledServiceProvider(TimeProvider.System);
        var runtime = services.GetRequiredService<ILocalSiteRuntimeStateProvider>();
        Assert.Equal(LocalSiteStateReadStatus.Missing, (await runtime.ReadAsync()).Status);
        await using var scope = services.CreateAsyncScope();
        await using var diagnosticsContext = database.CreateContext();
        var client = new SnapshotClient();
        var service = new SiteStateReconciliationService(client, new SiteStateSnapshotV1JsonParser(),
            scope.ServiceProvider.GetRequiredService<ISiteStateApplyService>(),
            new SiteStateSyncDiagnosticsStore(diagnosticsContext), new NoRetryPolicy(),
            Options.Create(new SiteStateReconciliationOptions { SiteId = 1 }), TimeProvider.System,
            NullLogger<SiteStateReconciliationService>.Instance);
        Assert.True((await service.ReconcileAsync(SiteStateReconciliationReason.Startup, CancellationToken.None)).Succeeded);
        Assert.Equal(3, (await runtime.ReadAsync()).Snapshot!.Revision);
        client.Revision = 4;
        Assert.True((await service.ReconcileAsync(SiteStateReconciliationReason.Periodic, CancellationToken.None)).Succeeded);
        Assert.Equal(4, (await runtime.ReadAsync()).Snapshot!.Revision);
        await runtime.ReadAsync();
        Assert.Equal(2, client.Calls);
    }

    [Fact]
    public async Task TwoProviders_RefreshAtTtl_AndRestartLazilyRecoversDurableState()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var clock = new FakeTimeProvider();
        var counter = new SnapshotQueryCounter();
        await using var a = database.CreateRetryEnabledServiceProvider(clock);
        await using var b = database.CreateRetryEnabledServiceProvider(clock, counter);
        await Apply(a, 1);
        var runtimeB = b.GetRequiredService<ILocalSiteRuntimeStateProvider>();
        Assert.Equal(0, counter.Count);
        Assert.Equal(1, (await runtimeB.ReadAsync()).Snapshot!.Revision);
        await Apply(a, 2);
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(1, (await runtimeB.ReadAsync()).Snapshot!.Revision);
        Assert.Equal(1, counter.Count);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, (await runtimeB.ReadAsync()).Snapshot!.Revision);
        Assert.Equal(2, counter.Count);
        var restartCounter = new SnapshotQueryCounter();
        await using var restarted = database.CreateRetryEnabledServiceProvider(clock, restartCounter);
        var restartedRuntime = restarted.GetRequiredService<ILocalSiteRuntimeStateProvider>();
        Assert.Equal(0, restartCounter.Count);
        Assert.Equal(2, (await restartedRuntime.ReadAsync()).Snapshot!.Revision);
        Assert.Equal(1, restartCounter.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealDurableRead_RacingApply_DiscardsOldResult(bool missing)
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var hold = new HoldRead();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<NeondbContext>(options => options.UseNpgsql(database.ConnectionString));
        services.AddScoped<ILocalSiteStateReader>(provider => new HeldReader(
            new LocalSiteStateReader(provider.GetRequiredService<NeondbContext>()), hold));
        services.AddLocalSiteRuntimeState();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        if (!missing) await Apply(provider, 1);
        var runtime = provider.GetRequiredService<ILocalSiteRuntimeStateProvider>();
        var readers = Enumerable.Range(0, 30).Select(_ => runtime.ReadAsync()).ToArray();
        await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Apply(provider, 2);
        hold.Release.SetResult();
        var results = await Task.WhenAll(readers);
        Assert.All(results, result => Assert.Equal(2, result.Snapshot!.Revision));
        Assert.Equal(2, hold.Reads);
    }

    [Fact]
    public async Task DurableOutcomes_KeepCacheAndRevisionStable()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var counter = new SnapshotQueryCounter();
        await using var services = database.CreateRetryEnabledServiceProvider(TimeProvider.System, counter);
        await Apply(services, 5);
        var runtime = services.GetRequiredService<ILocalSiteRuntimeStateProvider>();
        var cached = await runtime.ReadAsync();
        foreach (var (snapshot, expected) in new[]
        {
            (SiteStateContractTests.ValidSnapshot(revision: 5), SiteStateApplyOutcome.Equal),
            (SiteStateContractTests.ValidSnapshot(revision: 4), SiteStateApplyOutcome.Stale),
            (SiteStateContractTests.ValidSnapshot(revision: 5, siteName: "Conflict"), SiteStateApplyOutcome.EqualConflict)
        })
        {
            await using var scope = services.CreateAsyncScope();
            Assert.Equal(expected, (await scope.ServiceProvider.GetRequiredService<ISiteStateApplyService>().ApplyAsync(snapshot)).Outcome);
            var before = counter.Count;
            Assert.Same(cached, await runtime.ReadAsync());
            Assert.Equal(before, counter.Count);
        }
    }

    [Fact]
    public async Task Rollback_DoesNotInvalidateCachedMissing()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var counter = new SnapshotQueryCounter();
        await using var services = database.CreateRetryEnabledServiceProvider(TimeProvider.System, counter, new FailAfterSave());
        var runtime = services.GetRequiredService<ILocalSiteRuntimeStateProvider>();
        var cached = await runtime.ReadAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Apply(services, 1));
        var before = counter.Count;
        Assert.Same(cached, await runtime.ReadAsync());
        Assert.Equal(before, counter.Count);
        await using var verification = database.CreateContext();
        Assert.Empty(await verification.LocalSiteStateSnapshots.ToListAsync());
    }

    [Fact]
    public async Task CommitRetryAsDuplicate_InvalidatesMissing_WithoutIncomingPublication()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        await using var services = database.CreateRetryEnabledServiceProvider(TimeProvider.System, new FailOnceAfterCommit());
        var runtime = services.GetRequiredService<ILocalSiteRuntimeStateProvider>();
        Assert.Equal(LocalSiteStateReadStatus.Missing, (await runtime.ReadAsync()).Status);
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISiteStateApplyService>().ApplyAsync(
            SiteStateContractTests.ValidSnapshot(revision: 8), new SiteStateDeliveryContext(Guid.NewGuid(), new byte[32]));
        Assert.Equal(SiteStateApplyOutcome.DuplicateDelivery, result.Outcome);
        Assert.Equal(SiteStateReceiptDecision.Applied, result.OriginalDeliveryDecision);
        Assert.Equal(8, (await runtime.ReadAsync()).Snapshot!.Revision);
    }

    private static async Task<SiteStateApplyResult> Apply(IServiceProvider services, long revision)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISiteStateApplyService>()
            .ApplyAsync(SiteStateContractTests.ValidSnapshot(revision: revision));
    }

    private sealed class SnapshotQueryCounter : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains("local_site_state_snapshots"))
                Interlocked.Increment(ref Count);
            return ValueTask.FromResult(result);
        }
    }
    private sealed class HoldRead
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Reads;
    }
    private sealed class HeldReader(ILocalSiteStateReader reader, HoldRead hold) : ILocalSiteStateReader
    {
        public async Task<LocalSiteStateReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            var result = await reader.ReadAsync(cancellationToken);
            if (Interlocked.Increment(ref hold.Reads) == 1)
            {
                hold.Entered.SetResult();
                await hold.Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Rollback test");
    }
    private sealed class FailOnceAfterCommit : DbTransactionInterceptor
    {
        private int _commits;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _commits) == 1)
                throw new NpgsqlException("Simulated lost commit acknowledgement", new TimeoutException());
            return Task.CompletedTask;
        }
    }
    private sealed class SnapshotClient : IControlPanelSnapshotClient
    {
        public long Revision = 3;
        public int Calls;
        public Task<ControlPanelSnapshotClientResult> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new ControlPanelSnapshotClientResult(SiteStateWebhookEndpointTests.ValidBody(revision: Revision),
                ControlPanelSnapshotFailure.None, null));
        }
    }
    private sealed class NoRetryPolicy : ISiteStateReconciliationPolicy
    {
        public TimeSpan GetStartupDelay() => TimeSpan.Zero;
        public TimeSpan GetSuccessfulCycleDelay() => TimeSpan.FromMinutes(30);
        public TimeSpan GetFailedCycleDelay(int consecutiveFailures) => TimeSpan.FromMinutes(30);
        public TimeSpan? GetInlineRetryDelay(int nextAttempt, TimeSpan? retryAfter) => null;
    }
}
