using System.Data.Common;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

[Route("_yagot07/storefront")]
[ServiceFilter(typeof(SiteStatusFilter))]
public sealed class Yagot07StorefrontProbeController(
    Yagot07ActionRecorder recorder) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        recorder.Record();
        return Ok(new { success = true });
    }
}

[Area("Admin")]
[Route("Admin/_yagot07")]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public sealed class Yagot07AdminProbeController(
    Yagot07ActionRecorder recorder) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        recorder.Record();
        return Ok(new { success = true });
    }
}

public sealed class Yagot07ActionRecorder
{
    private int _count;
    public int Count => Volatile.Read(ref _count);
    public void Record() => Interlocked.Increment(ref _count);
    public void Reset() => Interlocked.Exchange(ref _count, 0);
}

internal sealed class Yagot07SnapshotQueryInterceptor : DbCommandInterceptor
{
    private int _count;
    private volatile bool _failReads;

    public int Count => Volatile.Read(ref _count);
    public bool FailReads
    {
        get => _failReads;
        set => _failReads = value;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (IsSnapshotRead(command))
        {
            Interlocked.Increment(ref _count);
            if (_failReads)
            {
                throw new InvalidOperationException("YAGOT07_READ_FAILURE_MARKER");
            }
        }
        return ValueTask.FromResult(result);
    }

    private static bool IsSnapshotRead(DbCommand command) =>
        command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
        command.CommandText.Contains("local_site_state_snapshots", StringComparison.OrdinalIgnoreCase);
}

internal sealed class Yagot07FailingSaveInterceptor : SaveChangesInterceptor
{
    public volatile bool Enabled;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (Enabled)
        {
            throw new Yagot07TemporaryDbException();
        }
        return ValueTask.FromResult(result);
    }
}

internal sealed class Yagot07TemporaryDbException : DbException
{
    public Yagot07TemporaryDbException() : base("YAGOT07_DB_FAILURE_MARKER") { }
}

internal sealed class Yagot07DiagnosticsCheckpointFailureInterceptor : DbCommandInterceptor
{
    private int _enabled;
    private int _failureCount;

    public bool Enabled
    {
        get => Volatile.Read(ref _enabled) == 1;
        set => Volatile.Write(ref _enabled, value ? 1 : 0);
    }

    public int FailureCount => Volatile.Read(ref _failureCount);

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (Enabled &&
            command.CommandText.Contains("site_state_sync_checkpoints", StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains("lastsuccessatutc = GREATEST", StringComparison.OrdinalIgnoreCase) &&
            Interlocked.Exchange(ref _enabled, 0) == 1)
        {
            Interlocked.Increment(ref _failureCount);
            throw new InvalidOperationException("YAGOT07_DIAGNOSTICS_FAILURE_MARKER");
        }

        return ValueTask.FromResult(result);
    }
}

internal sealed class Yagot07FailOnceAfterCommitInterceptor : DbTransactionInterceptor
{
    private int _enabled;
    private int _failureCount;

    public bool Enabled
    {
        get => Volatile.Read(ref _enabled) == 1;
        set => Volatile.Write(ref _enabled, value ? 1 : 0);
    }

    public int FailureCount => Volatile.Read(ref _failureCount);

    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _enabled, 0) == 1)
        {
            Interlocked.Increment(ref _failureCount);
            throw new NpgsqlException(
                "Simulated post-commit acknowledgement loss.",
                new TimeoutException("YAGOT07_POST_COMMIT_AMBIGUITY_MARKER"));
        }

        return Task.CompletedTask;
    }
}

internal sealed record Yagot07HostOptions(
    TimeProvider? Clock = null,
    TimeSpan? BackgroundStartupDelay = null,
    Yagot07ReadBarrier? ReadBarrier = null);

internal sealed class Yagot07BackgroundReconciliationPolicy : ISiteStateReconciliationPolicy
{
    private readonly TimeSpan _startupDelay;

    public Yagot07BackgroundReconciliationPolicy(TimeSpan startupDelay)
    {
        _startupDelay = startupDelay;
    }

    public TaskCompletionSource<TimeSpan> StartupDelayRequested { get; } = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public TimeSpan GetStartupDelay()
    {
        StartupDelayRequested.TrySetResult(_startupDelay);
        return _startupDelay;
    }

    public TimeSpan GetSuccessfulCycleDelay() => TimeSpan.FromHours(1);
    public TimeSpan GetFailedCycleDelay(int consecutiveFailures) => TimeSpan.FromHours(1);
    public TimeSpan? GetInlineRetryDelay(int nextAttempt, TimeSpan? retryAfter) => null;
}

internal sealed class Yagot07ReconciliationRunObserver
{
    public TaskCompletionSource<SiteStateReconciliationTriggerResult> Completed { get; } = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public SiteStateReconciliationReason? LastReason { get; private set; }

    public void Record(
        SiteStateReconciliationReason reason,
        SiteStateReconciliationTriggerResult result)
    {
        LastReason = reason;
        Completed.TrySetResult(result);
    }
}

internal sealed class Yagot07ObservingReconciliationCoordinator(
    SiteStateReconciliationCoordinator inner,
    Yagot07ReconciliationRunObserver observer) : ISiteStateReconciliationCoordinator
{
    public async Task<SiteStateReconciliationTriggerResult> RequestAsync(
        SiteStateReconciliationReason reason,
        CancellationToken cancellationToken)
    {
        var result = await inner.RequestAsync(reason, cancellationToken);
        observer.Record(reason, result);
        return result;
    }
}

internal sealed class Yagot07ReadBarrier
{
    private readonly object _gate = new();
    private TaskCompletionSource? _entered;
    private TaskCompletionSource? _release;

    public (Task Entered, Action Release) DelayNextRead()
    {
        lock (_gate)
        {
            _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = _release;
            return (_entered.Task, () => release.TrySetResult());
        }
    }

    public async Task WaitIfArmedAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource? entered;
        Task? release;
        lock (_gate)
        {
            entered = _entered;
            release = _release?.Task;
            _entered = null;
            _release = null;
        }

        if (release is not null)
        {
            entered!.TrySetResult();
            await release.WaitAsync(cancellationToken);
        }
    }
}

internal sealed class Yagot07HeldLocalSiteStateReader(
    LocalSiteStateReader inner,
    Yagot07ReadBarrier barrier) : ILocalSiteStateReader
{
    public async Task<LocalSiteStateReadResult> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        await barrier.WaitIfArmedAsync(cancellationToken);
        return await inner.ReadAsync(cancellationToken);
    }
}

internal static class Yagot07HttpAssertions
{
    public static async Task HasJsonCodeAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains($"\"code\":\"{code}\"", await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        Assert.Null(response.Headers.Location);
    }
}
