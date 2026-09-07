using Microsoft.Extensions.Options;
using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.Services.Integration;

public interface ISiteStateReconciliationService
{
    Task<SiteStateReconciliationResult> ReconcileAsync(
        SiteStateReconciliationReason reason,
        CancellationToken cancellationToken);
}

public sealed class SiteStateReconciliationService :
    ISiteStateReconciliationService
{
    private readonly IControlPanelSnapshotClient _snapshotClient;
    private readonly ISiteStateSnapshotV1JsonParser _parser;
    private readonly ISiteStateApplyService _applyService;
    private readonly ISiteStateSyncDiagnosticsStore _diagnostics;
    private readonly ISiteStateReconciliationPolicy _policy;
    private readonly SiteStateReconciliationOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SiteStateReconciliationService> _logger;

    public SiteStateReconciliationService(
        IControlPanelSnapshotClient snapshotClient,
        ISiteStateSnapshotV1JsonParser parser,
        ISiteStateApplyService applyService,
        ISiteStateSyncDiagnosticsStore diagnostics,
        ISiteStateReconciliationPolicy policy,
        IOptions<SiteStateReconciliationOptions> options,
        TimeProvider timeProvider,
        ILogger<SiteStateReconciliationService> logger)
    {
        _snapshotClient = snapshotClient;
        _parser = parser;
        _applyService = applyService;
        _diagnostics = diagnostics;
        _policy = policy;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<SiteStateReconciliationResult> ReconcileAsync(
        SiteStateReconciliationReason reason,
        CancellationToken cancellationToken)
    {
        var attemptedAtUtc = _timeProvider.GetUtcNow();
        await TryRecordAttemptAsync(attemptedAtUtc, cancellationToken);

        ControlPanelSnapshotClientResult clientResult;
        var httpAttempts = 0;
        while (true)
        {
            httpAttempts++;
            clientResult = await _snapshotClient.GetSnapshotAsync(cancellationToken);
            if (clientResult.Succeeded ||
                !clientResult.IsRetryable ||
                httpAttempts >= 3)
            {
                break;
            }

            var delay = _policy.GetInlineRetryDelay(
                httpAttempts + 1,
                clientResult.RetryAfter);
            if (delay is null)
            {
                break;
            }

            await Task.Delay(delay.Value, _timeProvider, cancellationToken);
        }

        if (!clientResult.Succeeded || clientResult.Body is null)
        {
            var failureCode = FailureCode(clientResult.Failure);
            await TryRecordFailureAsync(
                attemptedAtUtc,
                null,
                failureCode,
                cancellationToken);
            LogFailure(reason, failureCode, null, httpAttempts);
            return SiteStateReconciliationResult.Failure(
                failureCode,
                httpAttempts);
        }

        var parsed = _parser.Parse(clientResult.Body);
        if (parsed.Failure != SiteStateSnapshotParseFailure.None ||
            parsed.Snapshot is null)
        {
            var failureCode = parsed.Failure ==
                              SiteStateSnapshotParseFailure.MalformedJson
                ? SiteStateReconciliationFailureCodes.MalformedJson
                : SiteStateReconciliationFailureCodes.InvalidContract;
            await TryRecordFailureAsync(
                attemptedAtUtc,
                null,
                failureCode,
                cancellationToken);
            LogFailure(reason, failureCode, null, httpAttempts);
            return SiteStateReconciliationResult.Failure(
                failureCode,
                httpAttempts);
        }

        var snapshot = parsed.Snapshot;
        var validationFailure = ValidateSnapshot(snapshot);
        if (validationFailure is not null)
        {
            await TryRecordFailureAsync(
                attemptedAtUtc,
                snapshot.Revision >= 1 ? snapshot.Revision : null,
                validationFailure,
                cancellationToken);
            LogFailure(reason, validationFailure, snapshot.Revision, httpAttempts);
            return SiteStateReconciliationResult.Failure(
                validationFailure,
                httpAttempts,
                snapshot.Revision >= 1 ? snapshot.Revision : null);
        }

        SiteStateApplyResult applyResult;
        try
        {
            applyResult = await _applyService.ApplyAsync(
                snapshot,
                delivery: null,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failureCode = SiteStateReconciliationFailureCodes.ApplyFailure;
            await TryRecordFailureAsync(
                attemptedAtUtc,
                snapshot.Revision,
                failureCode,
                cancellationToken);
            _logger.LogError(
                "Site-state reconciliation apply failed; reason {Reason}; site {SiteId}; revision {Revision}; category {FailureCode}; failure type {FailureType}.",
                reason,
                snapshot.SiteId,
                snapshot.Revision,
                failureCode,
                exception.GetType().Name);
            return SiteStateReconciliationResult.Failure(
                failureCode,
                httpAttempts,
                snapshot.Revision);
        }

        if (applyResult.Outcome is SiteStateApplyOutcome.Applied or
            SiteStateApplyOutcome.Equal)
        {
            await TryRecordSuccessAsync(
                attemptedAtUtc,
                snapshot.Revision,
                cancellationToken);
            _logger.LogInformation(
                "Site-state reconciliation completed; reason {Reason}; site {SiteId}; revision {Revision}; outcome {Outcome}; attempts {HttpAttempts}.",
                reason,
                snapshot.SiteId,
                snapshot.Revision,
                applyResult.Outcome,
                httpAttempts);
            return SiteStateReconciliationResult.Success(
                applyResult.Outcome,
                snapshot.Revision,
                httpAttempts);
        }

        var anomalyCode = applyResult.Outcome switch
        {
            SiteStateApplyOutcome.Stale =>
                SiteStateReconciliationFailureCodes.RemoteRevisionOlder,
            SiteStateApplyOutcome.EqualConflict =>
                SiteStateReconciliationFailureCodes.EqualRevisionConflict,
            _ => SiteStateReconciliationFailureCodes.ApplyFailure
        };
        await TryRecordFailureAsync(
            attemptedAtUtc,
            snapshot.Revision,
            anomalyCode,
            cancellationToken);
        LogFailure(reason, anomalyCode, snapshot.Revision, httpAttempts);
        return SiteStateReconciliationResult.Failure(
            anomalyCode,
            httpAttempts,
            snapshot.Revision,
            applyResult.Outcome);
    }

    private string? ValidateSnapshot(SiteStateSnapshotV1 snapshot)
    {
        if (snapshot.ContractVersion != SiteStateContractV1.ContractVersion)
        {
            return SiteStateReconciliationFailureCodes.InvalidContractVersion;
        }

        if (snapshot.SiteId != _options.SiteId)
        {
            return SiteStateReconciliationFailureCodes.WrongSiteId;
        }

        if (!SiteStateContractV1.IsValidMode(snapshot.Mode))
        {
            return SiteStateReconciliationFailureCodes.InvalidMode;
        }

        try
        {
            snapshot.Validate();
            return null;
        }
        catch (SiteStateContractValidationException)
        {
            return SiteStateReconciliationFailureCodes.InvalidContract;
        }
    }

    private async Task TryRecordAttemptAsync(
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            await _diagnostics.RecordAttemptAsync(
                _options.SiteId,
                attemptedAtUtc,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogDiagnosticsFailure("attempt", exception);
        }
    }

    private async Task TryRecordSuccessAsync(
        DateTimeOffset attemptedAtUtc,
        long revision,
        CancellationToken cancellationToken)
    {
        try
        {
            await _diagnostics.RecordSuccessAsync(
                _options.SiteId,
                attemptedAtUtc,
                _timeProvider.GetUtcNow(),
                revision,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogDiagnosticsFailure("success", exception);
        }
    }

    private async Task TryRecordFailureAsync(
        DateTimeOffset attemptedAtUtc,
        long? revision,
        string failureCode,
        CancellationToken cancellationToken)
    {
        try
        {
            await _diagnostics.RecordFailureAsync(
                _options.SiteId,
                attemptedAtUtc,
                _timeProvider.GetUtcNow(),
                revision,
                failureCode,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogDiagnosticsFailure("failure", exception);
        }
    }

    private void LogDiagnosticsFailure(string operation, Exception exception) =>
        _logger.LogWarning(
            "Site-state reconciliation diagnostics write failed; operation {Operation}; failure type {FailureType}.",
            operation,
            exception.GetType().Name);

    private void LogFailure(
        SiteStateReconciliationReason reason,
        string failureCode,
        long? revision,
        int attempts) =>
        _logger.LogWarning(
            "Site-state reconciliation failed; reason {Reason}; site {SiteId}; revision {Revision}; category {FailureCode}; attempts {HttpAttempts}.",
            reason,
            _options.SiteId,
            revision,
            failureCode,
            attempts);

    private static string FailureCode(ControlPanelSnapshotFailure failure) =>
        failure switch
        {
            ControlPanelSnapshotFailure.Network =>
                SiteStateReconciliationFailureCodes.Network,
            ControlPanelSnapshotFailure.Timeout =>
                SiteStateReconciliationFailureCodes.Timeout,
            ControlPanelSnapshotFailure.Unauthorized =>
                SiteStateReconciliationFailureCodes.Unauthorized,
            ControlPanelSnapshotFailure.Forbidden =>
                SiteStateReconciliationFailureCodes.Forbidden,
            ControlPanelSnapshotFailure.NotFound =>
                SiteStateReconciliationFailureCodes.NotFound,
            ControlPanelSnapshotFailure.Throttled =>
                SiteStateReconciliationFailureCodes.Throttled,
            ControlPanelSnapshotFailure.ServerError =>
                SiteStateReconciliationFailureCodes.ServerError,
            ControlPanelSnapshotFailure.MissingNoStore =>
                SiteStateReconciliationFailureCodes.MissingNoStore,
            ControlPanelSnapshotFailure.InvalidMediaType =>
                SiteStateReconciliationFailureCodes.InvalidMediaType,
            ControlPanelSnapshotFailure.ResponseTooLarge =>
                SiteStateReconciliationFailureCodes.ResponseTooLarge,
            _ => SiteStateReconciliationFailureCodes.Protocol
        };
}
