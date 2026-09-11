namespace YAGOT_2._0.Services.Integration;

public interface ISiteStateSyncDiagnosticsStore
{
    Task RecordAttemptAsync(
        int siteId,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken);

    Task RecordSuccessAsync(
        int siteId,
        DateTimeOffset attemptedAtUtc,
        DateTimeOffset completedAtUtc,
        long remoteRevision,
        CancellationToken cancellationToken);

    Task RecordFailureAsync(
        int siteId,
        DateTimeOffset attemptedAtUtc,
        DateTimeOffset completedAtUtc,
        long? remoteRevision,
        string failureCode,
        CancellationToken cancellationToken);
}
