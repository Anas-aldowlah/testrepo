namespace YAGOT_2._0.Models;

public sealed class LocalCapabilitySnapshot
{
    public int SiteId { get; set; }
    public int ContractVersion { get; set; }
    public string CatalogVersion { get; set; } = null!;
    public long Revision { get; set; }
    public string SnapshotJson { get; set; } = null!;
    public byte[] PayloadSha256 { get; set; } = null!;
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public DateTimeOffset EffectiveAtUtc { get; set; }
    public DateTimeOffset AppliedAtUtc { get; set; }
}
