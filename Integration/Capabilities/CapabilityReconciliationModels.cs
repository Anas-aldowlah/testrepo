namespace YAGOT_2._0.Integration.Capabilities;

public enum CapabilityReconciliationReason
{
    Startup,
    Periodic,
    Manual
}

public sealed record CapabilityReconciliationResult(
    bool Succeeded,
    string Outcome,
    long? Revision,
    int Attempts,
    string? FailureCode = null)
{
    public static CapabilityReconciliationResult Success(string outcome, long revision, int attempts) =>
        new(true, outcome, revision, attempts);

    public static CapabilityReconciliationResult Failure(string failureCode, int attempts, long? revision = null) =>
        new(false, "Failure", revision, attempts, failureCode);
}
