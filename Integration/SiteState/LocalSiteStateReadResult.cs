namespace YAGOT_2._0.Integration.SiteState;

public enum LocalSiteStateReadStatus
{
    Missing,
    Found
}

public sealed record LocalSiteStateReadResult(
    LocalSiteStateReadStatus Status,
    SiteStateSnapshotV1? Snapshot)
{
    public static LocalSiteStateReadResult Missing { get; } =
        new(LocalSiteStateReadStatus.Missing, null);

    public static LocalSiteStateReadResult Found(SiteStateSnapshotV1 snapshot) =>
        new(LocalSiteStateReadStatus.Found, snapshot);
}
