namespace YAGOT_2._0.Integration.SiteState;

public enum SiteStateApplyOutcome
{
    Applied,
    Equal,
    EqualConflict,
    Stale,
    DuplicateDelivery,
    DeliveryIdPayloadConflict
}

public enum SiteStateReceiptDecision
{
    Applied,
    Equal,
    EqualConflict,
    Stale
}

public sealed record SiteStateApplyResult(
    SiteStateApplyOutcome Outcome,
    SiteStateReceiptDecision? OriginalDeliveryDecision = null);
