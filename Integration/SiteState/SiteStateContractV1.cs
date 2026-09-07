namespace YAGOT_2._0.Integration.SiteState;

public static class SiteStateContractV1
{
    public const int ContractVersion = 1;
    public const int SiteId = 1;
    public const int SiteNameMaxLength = 200;
    public const int PayloadSha256Length = 32;

    public const string Online = "Online";
    public const string Development = "Development";
    public const string Offline = "Offline";

    public static bool IsValidMode(string? mode) =>
        mode is Online or Development or Offline;
}
