namespace YAGOT_2._0.Models;

public sealed class SiteStateSyncCheckpoint
{
    public int SiteId { get; set; }
    public DateTimeOffset LastAttemptAtUtc { get; set; }
    public DateTimeOffset? LastSuccessAtUtc { get; set; }
    public long? LastObservedRemoteRevision { get; set; }
    public int ConsecutiveFailures { get; set; }
    public string? LastFailureCode { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
