namespace YAGOT_2._0.Integration.Capabilities;

public static class CapabilityContractV1
{
    public const int ContractVersion = 1;
    public const string CatalogVersion = "yaqoot-capabilities-1";
    public const int PayloadSha256Length = 32;
}

public sealed class CapabilityIntegrationOptions
{
    public const string SectionName = "CapabilityIntegration";
    public int SiteId { get; set; } = 1;
}
