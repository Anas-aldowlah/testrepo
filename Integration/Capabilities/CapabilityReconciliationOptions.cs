namespace YAGOT_2._0.Integration.Capabilities;

public sealed class CapabilityReconciliationOptions
{
    public const string SectionName = "YagotIntegration";
    public const int ApprovedIntervalMinutes = 30;
    public const int ApprovedHttpTimeoutSeconds = 10;
    public const int ResponseSizeLimitBytes = 64 * 1024;

    public int SiteId { get; set; } = 1;
    public string? ControlPanelBaseUrl { get; set; }
    public string? SnapshotApiKey { get; set; }
    public int ReconciliationIntervalMinutes { get; set; } = ApprovedIntervalMinutes;
    public int SnapshotHttpTimeoutSeconds { get; set; } = ApprovedHttpTimeoutSeconds;
}
