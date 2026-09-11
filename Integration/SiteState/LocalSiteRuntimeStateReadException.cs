namespace YAGOT_2._0.Integration.SiteState;

public enum LocalSiteRuntimeStateReadFailure
{
    StorageUnavailable,
    LoadTimeout,
    InvalidDurableState,
    RevisionRegression,
    EqualRevisionConflict,
    ProviderStopped
}

public sealed class LocalSiteRuntimeStateReadException : Exception
{
    public LocalSiteRuntimeStateReadFailure Category { get; }

    public LocalSiteRuntimeStateReadException(LocalSiteRuntimeStateReadFailure category)
        : base($"Local site-state read failed: {category}.")
    {
        Category = category;
    }
}
