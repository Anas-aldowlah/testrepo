using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.Services.Integration;

internal sealed class LocalSiteRuntimeStateProvider(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<LocalSiteRuntimeStateProvider> logger)
    : ILocalSiteRuntimeStateProvider, ILocalSiteRuntimeStateInvalidator, IAsyncDisposable
{
    private static readonly TimeSpan Freshness = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FailureSuppression = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan LoadBudget = TimeSpan.FromSeconds(5);
    private readonly object _gate = new();
    private readonly CancellationTokenSource _shutdown = new();
    // One immutable entry. The last observed snapshot is retained only as an
    // integrity guard if a later durable read reports Missing; it is never served as fallback.
    private sealed record CacheEntry(LocalSiteStateReadResult Result, SiteStateSnapshotV1? LastKnownSnapshot);
    private CacheEntry? _entry;
    private long _loadedAt;
    private bool _eligible;
    private long _generation;
    private long _revisionFloor;
    private TaskCompletionSource<LocalSiteStateReadResult>? _flight;
    private LocalSiteRuntimeStateReadFailure? _failure;
    private long _failedAt;
    private bool _stopped;

    public Task<LocalSiteStateReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TaskCompletionSource<LocalSiteStateReadResult>? start = null;
        Task<LocalSiteStateReadResult> task;
        lock (_gate)
        {
            if (_stopped)
                return Task.FromException<LocalSiteStateReadResult>(Failure(LocalSiteRuntimeStateReadFailure.ProviderStopped));
            if (_eligible && _entry is not null &&
                timeProvider.GetElapsedTime(_loadedAt) < Freshness)
                return Task.FromResult(_entry.Result);
            if (_failure is { } failure &&
                timeProvider.GetElapsedTime(_failedAt) < FailureSuppression)
                return Task.FromException<LocalSiteStateReadResult>(Failure(failure));

            if (_flight is null)
            {
                start = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _flight = start;
            }
            task = _flight.Task;
        }

        // Start outside the lock: an async reader may execute synchronously before its first await.
        if (start is not null)
            _ = LoadAsync(start);
        return task.WaitAsync(cancellationToken);
    }

    public void ObserveCommittedRevision(long revision)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(revision, 1);
        lock (_gate)
        {
            if (_stopped) return;
            _revisionFloor = Math.Max(_revisionFloor, revision);
            if (_entry?.Result.Snapshot is { } cached && cached.Revision >= revision)
                return; // In particular, Equal never renews the freshness timestamp.
            InvalidateUnderLock();
        }
    }

    public void InvalidateForCommittedDelivery()
    {
        lock (_gate)
        {
            if (!_stopped) InvalidateUnderLock();
        }
    }

    private void InvalidateUnderLock()
    {
        _generation++;
        _eligible = false;
        _failure = null;
        // Keep the immutable value for revision/equal-payload checks; never serve it stale.
    }

    private async Task LoadAsync(TaskCompletionSource<LocalSiteStateReadResult> completion)
    {
        try
        {
            using var budget = new CancellationTokenSource(LoadBudget, timeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(budget.Token, _shutdown.Token);
            var token = linked.Token;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                long generation;
                long startedAt;
                lock (_gate)
                {
                    if (_stopped) throw Failure(LocalSiteRuntimeStateReadFailure.ProviderStopped);
                    generation = _generation;
                    startedAt = timeProvider.GetTimestamp();
                }

                LocalSiteStateReadResult result;
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    result = await scope.ServiceProvider.GetRequiredService<ILocalSiteStateReader>()
                        .ReadAsync(token);
                }

                lock (_gate)
                {
                    if (_stopped) throw Failure(LocalSiteRuntimeStateReadFailure.ProviderStopped);
                    token.ThrowIfCancellationRequested();
                    if (generation != _generation) continue;
                    ValidateUnderLock(result);
                    if (timeProvider.GetElapsedTime(startedAt) >= Freshness) continue;

                    _entry = new CacheEntry(result, result.Snapshot ?? _entry?.LastKnownSnapshot);
                    _revisionFloor = Math.Max(_revisionFloor, result.Snapshot?.Revision ?? 0);
                    _loadedAt = startedAt;
                    _eligible = true;
                    _failure = null;
                    _flight = null;
                    completion.TrySetResult(result);
                    return;
                }
            }
        }
        catch (Exception exception)
        {
            LocalSiteRuntimeStateReadFailure category;
            lock (_gate)
            {
                category = _stopped ? LocalSiteRuntimeStateReadFailure.ProviderStopped
                    : exception is LocalSiteRuntimeStateReadException known ? known.Category
                    : exception is OperationCanceledException ? LocalSiteRuntimeStateReadFailure.LoadTimeout
                    : exception is SiteStateContractValidationException ? LocalSiteRuntimeStateReadFailure.InvalidDurableState
                    : LocalSiteRuntimeStateReadFailure.StorageUnavailable;
                _failure = category;
                _failedAt = timeProvider.GetTimestamp();
                _flight = null;
                completion.TrySetException(Failure(category));
            }
            try
            {
                logger.LogWarning("Local site-state read failed; category {FailureCategory}.", category);
            }
            catch (Exception) { }
        }
    }

    private void ValidateUnderLock(LocalSiteStateReadResult result)
    {
        if (result is null ||
            (result.Status == LocalSiteStateReadStatus.Missing && result.Snapshot is not null) ||
            (result.Status != LocalSiteStateReadStatus.Missing && result.Status != LocalSiteStateReadStatus.Found))
            throw Failure(LocalSiteRuntimeStateReadFailure.InvalidDurableState);

        if (result.Status == LocalSiteStateReadStatus.Missing)
        {
            return;
        }
        var snapshot = result.Snapshot ?? throw Failure(LocalSiteRuntimeStateReadFailure.InvalidDurableState);
        snapshot.Validate();
        if (snapshot.Revision < _revisionFloor)
            throw Failure(LocalSiteRuntimeStateReadFailure.RevisionRegression);
        if (_entry?.LastKnownSnapshot is { } previous && previous.Revision == snapshot.Revision &&
            !previous.LogicallyEquals(snapshot))
            throw Failure(LocalSiteRuntimeStateReadFailure.EqualRevisionConflict);
    }

    private static LocalSiteRuntimeStateReadException Failure(LocalSiteRuntimeStateReadFailure category) => new(category);

    public async ValueTask DisposeAsync()
    {
        Task? pending;
        lock (_gate)
        {
            if (_stopped) return;
            _stopped = true;
            _eligible = false;
            pending = _flight?.Task;
        }
        await _shutdown.CancelAsync();
        if (pending is not null)
        {
            try { await pending; }
            catch (LocalSiteRuntimeStateReadException) { }
        }
        _shutdown.Dispose();
    }
}
