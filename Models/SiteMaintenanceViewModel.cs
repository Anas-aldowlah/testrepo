namespace YAGOT_2._0.Models;

public sealed record SiteMaintenanceViewModel(
    string SiteName,
    string SiteUrlText,
    Uri? ClickableSiteUrl,
    DateOnly StartDate,
    DateOnly EndDate,
    int OriginalDurationDays);
