using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.Services.Integration;

public interface ICapabilityReconciliationService
{
    Task<CapabilityReconciliationResult> ReconcileAsync(
        CapabilityReconciliationReason reason,
        CancellationToken cancellationToken = default);
}

public sealed class CapabilityReconciliationService(
    IControlPanelCapabilitySnapshotClient client,
    ICapabilitySnapshotV1JsonParser parser,
    ICapabilityApplyService applyService,
    IOptions<CapabilityReconciliationOptions> options,
    TimeProvider timeProvider,
    ILogger<CapabilityReconciliationService> logger) : ICapabilityReconciliationService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<CapabilityReconciliationResult> ReconcileAsync(
        CapabilityReconciliationReason reason,
        CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            logger.LogDebug("Capability reconciliation skipped; another cycle is already executing.");
            return CapabilityReconciliationResult.Success("AlreadyRunning", 0, 0);
        }

        try
        {
            var attempts = 0;
            ControlPanelSnapshotClientResult clientResult;
            while (true)
            {
                attempts++;
                clientResult = await client.GetSnapshotAsync(cancellationToken);
                if (clientResult.Succeeded || !clientResult.IsRetryable || attempts >= 3)
                {
                    break;
                }

                var delay = clientResult.RetryAfter ?? TimeSpan.FromSeconds(Math.Min(10, Math.Pow(2, attempts)));
                await Task.Delay(delay, timeProvider, cancellationToken);
            }

            if (!clientResult.Succeeded || clientResult.Body is null)
            {
                var failureCode = clientResult.Failure.ToString();
                logger.LogWarning(
                    "Capability reconciliation failed; reason {Reason}; site {SiteId}; category {FailureCode}; attempts {Attempts}.",
                    reason,
                    options.Value.SiteId,
                    failureCode,
                    attempts);
                return CapabilityReconciliationResult.Failure(failureCode, attempts);
            }

            var parsed = parser.Parse(clientResult.Body);
            if (parsed.Failure != CapabilitySnapshotParseFailure.None || parsed.Snapshot is null)
            {
                var failureCode = parsed.Failure.ToString();
                logger.LogWarning(
                    "Capability reconciliation failed; reason {Reason}; site {SiteId}; category {FailureCode}; attempts {Attempts}; detail: {Diagnostic}.",
                    reason,
                    options.Value.SiteId,
                    failureCode,
                    attempts,
                    parsed.Diagnostic);
                return CapabilityReconciliationResult.Failure(failureCode, attempts);
            }

            var snapshot = parsed.Snapshot;
            var applyResult = await applyService.ApplyAsync(snapshot, delivery: null, cancellationToken);

            if (applyResult.Outcome is CapabilityApplyOutcome.Applied or CapabilityApplyOutcome.Equal)
            {
                logger.LogInformation(
                    "Capability reconciliation completed; reason {Reason}; site {SiteId}; revision {Revision}; outcome {Outcome}; attempts {Attempts}.",
                    reason,
                    snapshot.SiteId,
                    snapshot.Revision,
                    applyResult.Outcome,
                    attempts);
                return CapabilityReconciliationResult.Success(
                    applyResult.Outcome.ToString(),
                    snapshot.Revision,
                    attempts);
            }

            logger.LogWarning(
                "Capability reconciliation anomaly; reason {Reason}; site {SiteId}; revision {Revision}; outcome {Outcome}; attempts {Attempts}.",
                reason,
                snapshot.SiteId,
                snapshot.Revision,
                applyResult.Outcome,
                attempts);
            return CapabilityReconciliationResult.Failure(
                applyResult.Outcome.ToString(),
                attempts,
                snapshot.Revision);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Capability reconciliation failed unexpectedly; reason {Reason}; site {SiteId}.",
                reason,
                options.Value.SiteId);
            return CapabilityReconciliationResult.Failure(exception.GetType().Name, 1);
        }
        finally
        {
            _gate.Release();
        }
    }
}
