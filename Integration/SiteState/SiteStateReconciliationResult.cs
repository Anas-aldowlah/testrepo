namespace YAGOT_2._0.Integration.SiteState;

public static class SiteStateReconciliationFailureCodes
{
    public const string Network = "network_error";
    public const string Timeout = "timeout";
    public const string Unauthorized = "http_401";
    public const string Forbidden = "http_403";
    public const string NotFound = "http_404";
    public const string Throttled = "http_429";
    public const string ServerError = "http_5xx";
    public const string Protocol = "protocol_error";
    public const string MissingNoStore = "missing_no_store";
    public const string InvalidMediaType = "invalid_media_type";
    public const string ResponseTooLarge = "response_too_large";
    public const string MalformedJson = "malformed_json";
    public const string InvalidContract = "invalid_contract";
    public const string InvalidContractVersion = "invalid_contract_version";
    public const string WrongSiteId = "wrong_site_id";
    public const string InvalidMode = "invalid_mode";
    public const string RemoteRevisionOlder = "remote_revision_older";
    public const string EqualRevisionConflict = "equal_revision_conflict";
    public const string ApplyFailure = "apply_failure";
}

public sealed record SiteStateReconciliationResult(
    bool Succeeded,
    string? FailureCode,
    SiteStateApplyOutcome? ApplyOutcome,
    long? RemoteRevision,
    int HttpAttempts)
{
    public static SiteStateReconciliationResult Success(
        SiteStateApplyOutcome outcome,
        long revision,
        int httpAttempts) =>
        new(true, null, outcome, revision, httpAttempts);

    public static SiteStateReconciliationResult Failure(
        string failureCode,
        int httpAttempts,
        long? remoteRevision = null,
        SiteStateApplyOutcome? applyOutcome = null) =>
        new(false, failureCode, applyOutcome, remoteRevision, httpAttempts);
}

public enum SiteStateReconciliationReason
{
    Startup,
    Periodic,
    RevisionGap,
    OperatorRecovery
}

public enum SiteStateReconciliationTriggerStatus
{
    Completed,
    AlreadyRunning
}

public sealed record SiteStateReconciliationTriggerResult(
    SiteStateReconciliationTriggerStatus Status,
    SiteStateReconciliationResult? Result);
