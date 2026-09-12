namespace YAGOT_2._0.Models;

public sealed class CapabilityEventReceipt
{
    public Guid DeliveryId { get; set; }
    public int SiteId { get; set; }
    public long Revision { get; set; }
    public byte[] PayloadSha256 { get; set; } = null!;
    public string Decision { get; set; } = null!;
    public DateTimeOffset RecordedAtUtc { get; set; }
}
