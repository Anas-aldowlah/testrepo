namespace YAGOT_2._0.Services.Caching;

public sealed class BackendCacheOptions
{
    public const string SectionName = "BackendCache";

    public int HomeMinutes { get; set; } = 10;
    public int CatalogMinutes { get; set; } = 10;
    public int CategoriesMinutes { get; set; } = 15;
    public int SettingsMinutes { get; set; } = 15;

    public TimeSpan HomeDuration => TimeSpan.FromMinutes(Math.Clamp(HomeMinutes, 1, 1440));
    public TimeSpan CatalogDuration => TimeSpan.FromMinutes(Math.Clamp(CatalogMinutes, 1, 1440));
    public TimeSpan CategoriesDuration => TimeSpan.FromMinutes(Math.Clamp(CategoriesMinutes, 1, 1440));
    public TimeSpan SettingsDuration => TimeSpan.FromMinutes(Math.Clamp(SettingsMinutes, 1, 1440));
}
