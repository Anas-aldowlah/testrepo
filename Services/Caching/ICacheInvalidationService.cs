namespace YAGOT_2._0.Services.Caching;

public interface ICacheInvalidationService
{
    void InvalidateHomeShowcase();
    void InvalidateCatalogMetadata();
    void InvalidateCategoriesList();
    void InvalidateStoreSettings();
    void InvalidateAll();

    long HomeVersion { get; }
    long CatalogVersion { get; }
    long CategoriesVersion { get; }
}
