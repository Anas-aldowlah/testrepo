namespace YAGOT_2._0.Integration.Capabilities;

public enum CapabilityApplyOutcome { Applied, Equal, Stale, DuplicateDelivery, Conflict, EqualConflict, Rejected }
public enum CapabilityReceiptDecision { Applied, Equal, Stale, EqualConflict }
public enum CapabilityHealthStatus { NoSnapshot, Healthy, Degraded, InvalidSnapshot, StaleRemote, RevisionConflict, StorageFailure }

public sealed record CapabilityApplyResult(
    CapabilityApplyOutcome Outcome,
    CapabilityHealthStatus Health,
    CapabilityReceiptDecision? OriginalDeliveryDecision = null,
    string? Diagnostic = null);

public sealed record CapabilityDeliveryContext(Guid DeliveryId)
{
    public void Validate()
    {
        if (DeliveryId == Guid.Empty) throw new CapabilityContractValidationException("DeliveryId is required.");
    }
}

public interface ICapabilityApplyService
{
    Task<CapabilityApplyResult> ApplyAsync(CapabilitySnapshotV1 snapshot, CapabilityDeliveryContext? delivery = null, CancellationToken cancellationToken = default);
}
