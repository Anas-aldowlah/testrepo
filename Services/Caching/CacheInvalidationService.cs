using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace YAGOT_2._0.Services.Caching;

public sealed class CacheInvalidationService : ICacheInvalidationService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheInvalidationService> _logger;

    private long _homeVersion = 1;
    private long _catalogVersion = 1;
    private long _categoriesVersion = 1;

    public CacheInvalidationService(
        IMemoryCache cache,
        ILogger<CacheInvalidationService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public long HomeVersion => Volatile.Read(ref _homeVersion);
    public long CatalogVersion => Volatile.Read(ref _catalogVersion);
    public long CategoriesVersion => Volatile.Read(ref _categoriesVersion);

    public void InvalidateHomeShowcase()
    {
        Interlocked.Increment(ref _homeVersion);
        _cache.Remove(StorefrontCacheKeys.HomeShowcase);
        _logger.LogInformation("Cache invalidated: HomeShowcase (New version: {Version}).", HomeVersion);
    }

    public void InvalidateCatalogMetadata()
    {
        Interlocked.Increment(ref _catalogVersion);
        _cache.Remove(StorefrontCacheKeys.CatalogMetadata);
        _logger.LogInformation("Cache invalidated: CatalogMetadata (New version: {Version}).", CatalogVersion);
    }

    public void InvalidateCategoriesList()
    {
        Interlocked.Increment(ref _categoriesVersion);
        _cache.Remove(StorefrontCacheKeys.CategoriesList);
        _logger.LogInformation("Cache invalidated: CategoriesList (New version: {Version}).", CategoriesVersion);
    }

    public void InvalidateStoreSettings()
    {
        _cache.Remove(StorefrontCacheKeys.StoreSettings);
        _cache.Remove(StorefrontCacheKeys.FooterSettings);
        _logger.LogInformation("Cache invalidated: StoreSettings and FooterSettings.");
    }

    public void InvalidateAll()
    {
        InvalidateHomeShowcase();
        InvalidateCatalogMetadata();
        InvalidateCategoriesList();
        InvalidateStoreSettings();
        _logger.LogInformation("Cache invalidated: ALL storefront caches purged.");
    }
}
