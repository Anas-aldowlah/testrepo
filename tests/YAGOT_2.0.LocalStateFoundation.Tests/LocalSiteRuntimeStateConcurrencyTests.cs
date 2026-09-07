using YAGOT_2._0.Integration.SiteState;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class LocalSiteRuntimeStateConcurrencyTests
{
    [Fact]
    public async Task ManyMisses_ShareOneLoad_CallerCancellationIsIsolated()
    {
        await using var rig = new RuntimeTestRig();
        var release = new TaskCompletionSource<LocalSiteStateReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken sharedToken = default;
        rig.Read = token => { sharedToken = token; return release.Task; };
        using var caller = new CancellationTokenSource();
        var canceled = rig.Runtime.ReadAsync(caller.Token);
        var reads = Enumerable.Range(0, 100).Select(_ => rig.Runtime.ReadAsync()).ToArray();
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        Assert.False(sharedToken.IsCancellationRequested);
        release.SetResult(RuntimeTestRig.Found(1));
        await Task.WhenAll(reads);
        Assert.Equal(1, rig.Reads);
        Assert.All(reads, read => Assert.Equal(1, read.Result.Snapshot!.Revision));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidationDuringRead_DiscardsOldFoundOrMissing(bool missing)
    {
        await using var rig = new RuntimeTestRig();
        var release = new TaskCompletionSource<LocalSiteStateReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Read = _ => rig.Reads == 1 ? release.Task : Task.FromResult(RuntimeTestRig.Found(2));
        var read = rig.Runtime.ReadAsync();
        rig.Invalidator.ObserveCommittedRevision(2);
        rig.Invalidator.ObserveCommittedRevision(1);
        release.SetResult(missing ? LocalSiteStateReadResult.Missing : RuntimeTestRig.Found(1));
        Assert.Equal(2, (await read).Snapshot!.Revision);
        Assert.Equal(2, rig.Reads);
    }

    [Fact]
    public async Task LoadTimestamp_IsAttemptStart_NotCompletion()
    {
        await using var rig = new RuntimeTestRig();
        var release = new TaskCompletionSource<LocalSiteStateReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Read = _ => release.Task;
        var read = rig.Runtime.ReadAsync();
        rig.Clock.Advance(TimeSpan.FromSeconds(4));
        release.SetResult(RuntimeTestRig.Found(1));
        await read;
        rig.Clock.Advance(TimeSpan.FromSeconds(26));
        await rig.Runtime.ReadAsync();
        Assert.Equal(2, rig.Reads);
    }

    [Fact]
    public async Task FiveSecondBudget_CancelsSharedDatabaseRead()
    {
        await using var rig = new RuntimeTestRig();
        rig.Read = async token => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return RuntimeTestRig.Found(1); };
        var read = rig.Runtime.ReadAsync();
        rig.Clock.Advance(TimeSpan.FromSeconds(5));
        var failure = await Assert.ThrowsAsync<LocalSiteRuntimeStateReadException>(() => read);
        Assert.Equal(LocalSiteRuntimeStateReadFailure.LoadTimeout, failure.Category);
    }

    [Fact]
    public async Task Shutdown_PreventsLatePublication()
    {
        var rig = new RuntimeTestRig();
        var release = new TaskCompletionSource<LocalSiteStateReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Read = _ => release.Task;
        var read = rig.Runtime.ReadAsync();
        var stopped = rig.DisposeAsync().AsTask();
        release.SetResult(RuntimeTestRig.Found(1));
        var failure = await Assert.ThrowsAsync<LocalSiteRuntimeStateReadException>(() => read);
        Assert.Equal(LocalSiteRuntimeStateReadFailure.ProviderStopped, failure.Category);
        await stopped;
        Assert.Equal(1, rig.DisposedReaders);
    }
}
