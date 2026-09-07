using YAGOT_2._0.Models;

namespace YAGOT_2._0.Integration.SiteState;

public enum SiteAccessSurface
{
    Storefront,
    Admin
}

public enum SiteAccessDecisionKind
{
    Allow,
    DevelopmentRestricted,
    OfflineRestricted,
    Missing,
    StorageUnavailable,
    LoadTimeout,
    InvalidDurableState,
    RevisionRegression,
    EqualRevisionConflict,
    ProviderUnavailable
}

public enum SiteAccessHtmlTarget
{
    None,
    Developer,
    Close,
    Unavailable
}

public sealed record SiteAccessDecision(
    SiteAccessDecisionKind Kind,
    SiteAccessHtmlTarget HtmlTarget,
    string? EffectiveMode,
    SiteStateSnapshotV1? Snapshot,
    SiteMaintenanceViewModel? Maintenance)
{
    public bool IsAllowed => Kind == SiteAccessDecisionKind.Allow;

    public string Code => Kind switch
    {
        SiteAccessDecisionKind.DevelopmentRestricted => "site_development",
        SiteAccessDecisionKind.OfflineRestricted => "site_offline",
        SiteAccessDecisionKind.Missing => "site_state_missing",
        SiteAccessDecisionKind.StorageUnavailable => "site_state_storage_unavailable",
        SiteAccessDecisionKind.LoadTimeout => "site_state_read_timeout",
        SiteAccessDecisionKind.InvalidDurableState => "site_state_invalid",
        SiteAccessDecisionKind.RevisionRegression => "site_state_revision_regression",
        SiteAccessDecisionKind.EqualRevisionConflict => "site_state_equal_revision_conflict",
        SiteAccessDecisionKind.ProviderUnavailable => "site_state_provider_unavailable",
        _ => "site_access_allowed"
    };
}
