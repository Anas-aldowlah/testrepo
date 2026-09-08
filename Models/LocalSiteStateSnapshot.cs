using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.Models;

public sealed class LocalSiteStateSnapshot
{
    public int SiteId { get; set; }
    public int ContractVersion { get; set; }
    public string Mode { get; set; } = null!;
    public long Revision { get; set; }
    public DateTimeOffset EffectiveAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string SiteName { get; set; } = null!;
    public string SiteUrl { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public int OriginalDurationDays { get; set; }

    public SiteStateSnapshotV1 ToContract() => new(
        ContractVersion,
        SiteId,
        Mode,
        Revision,
        EffectiveAtUtc.ToUniversalTime(),
        ExpiresAtUtc.ToUniversalTime(),
        SiteName,
        SiteUrl,
        StartDate,
        OriginalDurationDays);

    public void Apply(SiteStateSnapshotV1 snapshot)
    {
        SiteId = snapshot.SiteId;
        ContractVersion = snapshot.ContractVersion;
        Mode = snapshot.Mode;
        Revision = snapshot.Revision;
        EffectiveAtUtc = snapshot.EffectiveAtUtc.ToUniversalTime();
        ExpiresAtUtc = snapshot.ExpiresAtUtc.ToUniversalTime();
        SiteName = snapshot.SiteName;
        SiteUrl = snapshot.SiteUrl;
        StartDate = snapshot.StartDate;
        OriginalDurationDays = snapshot.OriginalDurationDays;
    }

    public static LocalSiteStateSnapshot FromContract(SiteStateSnapshotV1 snapshot)
    {
        var entity = new LocalSiteStateSnapshot();
        entity.Apply(snapshot);
        return entity;
    }
}
