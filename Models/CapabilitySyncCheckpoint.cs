namespace YAGOT_2._0.Models;

public sealed class CapabilitySyncCheckpoint
{
    public int SiteId { get; set; }
    public long? LastAttemptedRevision { get; set; }
    public long? LastObservedRemoteRevision { get; set; }
    public long? LastSuccessfullyAppliedRevision { get; set; }
    public DateTimeOffset LastAttemptAtUtc { get; set; }
    public DateTimeOffset? LastSuccessAtUtc { get; set; }
    public string Health { get; set; } = null!;
    public string? LastFailureCategory { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
