namespace YAGOT_2._0.Integration.SiteState;

public sealed record SiteStateSnapshotV1(
    int ContractVersion,
    int SiteId,
    string Mode,
    long Revision,
    DateTimeOffset EffectiveAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string SiteName,
    string SiteUrl,
    DateOnly StartDate,
    int OriginalDurationDays)
{
    public void Validate()
    {
        if (ContractVersion != SiteStateContractV1.ContractVersion)
        {
            throw new SiteStateContractValidationException(
                $"ContractVersion must be {SiteStateContractV1.ContractVersion}.");
        }

        if (SiteId != SiteStateContractV1.SiteId)
        {
            throw new SiteStateContractValidationException(
                $"SiteId must be {SiteStateContractV1.SiteId}.");
        }

        if (!SiteStateContractV1.IsValidMode(Mode))
        {
            throw new SiteStateContractValidationException(
                "Mode must be Online, Development, or Offline using exact casing.");
        }

        if (Revision < 1)
        {
            throw new SiteStateContractValidationException("Revision must be at least 1.");
        }

        if (EffectiveAtUtc.Offset != TimeSpan.Zero)
        {
            throw new SiteStateContractValidationException("EffectiveAtUtc must be UTC.");
        }

        if (ExpiresAtUtc.Offset != TimeSpan.Zero)
        {
            throw new SiteStateContractValidationException("ExpiresAtUtc must be UTC.");
        }

        if (SiteName is null)
        {
            throw new SiteStateContractValidationException("SiteName is required.");
        }

        if (SiteName.Length > SiteStateContractV1.SiteNameMaxLength)
        {
            throw new SiteStateContractValidationException(
                $"SiteName cannot exceed {SiteStateContractV1.SiteNameMaxLength} characters.");
        }

        if (SiteUrl is null)
        {
            throw new SiteStateContractValidationException("SiteUrl is required.");
        }

        if (OriginalDurationDays <= 0)
        {
            throw new SiteStateContractValidationException(
                "OriginalDurationDays must be greater than zero.");
        }
    }

    public bool LogicallyEquals(SiteStateSnapshotV1 other) =>
        ContractVersion == other.ContractVersion &&
        SiteId == other.SiteId &&
        string.Equals(Mode, other.Mode, StringComparison.Ordinal) &&
        Revision == other.Revision &&
        EffectiveAtUtc.EqualsExact(other.EffectiveAtUtc) &&
        ExpiresAtUtc.EqualsExact(other.ExpiresAtUtc) &&
        string.Equals(SiteName, other.SiteName, StringComparison.Ordinal) &&
        string.Equals(SiteUrl, other.SiteUrl, StringComparison.Ordinal) &&
        StartDate == other.StartDate &&
        OriginalDurationDays == other.OriginalDurationDays;
}
