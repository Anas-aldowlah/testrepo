using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services.Caching.Dtos;

namespace YAGOT_2._0.Services.Caching;

public sealed class StorefrontCacheService : IStorefrontCacheService
{
    private readonly NeondbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ICacheInvalidationService _invalidationService;
    private readonly BackendCacheOptions _options;
    private readonly ILogger<StorefrontCacheService> _logger;

    private static readonly SemaphoreSlim HomeLock = new(1, 1);
    private static readonly SemaphoreSlim CategoriesLock = new(1, 1);

    public StorefrontCacheService(
        NeondbContext context,
        IMemoryCache cache,
        ICacheInvalidationService invalidationService,
        IOptions<BackendCacheOptions> options,
        ILogger<StorefrontCacheService> logger)
    {
        _context = context;
        _cache = cache;
        _invalidationService = invalidationService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HomeShowcaseDto> GetHomeShowcaseAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(StorefrontCacheKeys.HomeShowcase, out HomeShowcaseDto? cached) && cached != null)
        {
            return cached;
        }

        await HomeLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(StorefrontCacheKeys.HomeShowcase, out cached) && cached != null)
            {
                return cached;
            }

            var loadVersion = _invalidationService.HomeVersion;

            // 1. New Arrivals
            var newArrivals = await ProjectToProductCardDto(
                _context.Products
                    .AsNoTracking()
                    .OrderByDescending(p => p.Createdat)
                    .Take(8))
                .ToListAsync(cancellationToken);

            // 2. Hero Slides
            var heroSlides = await _context.Products
                .AsNoTracking()
                .Where(p => p.Imageurl != null && p.Imageurl.Trim() != string.Empty)
                .OrderByDescending(p => p.Createdat)
                .Take(12)
                .Select(p => new HeroSlideDto(
                    p.Id,
                    p.Name,
                    p.Imageurl,
                    p.Createdat))
                .ToListAsync(cancellationToken);

            if (heroSlides.Count == 0 && newArrivals.Count > 0)
            {
                heroSlides = newArrivals
                    .Take(6)
                    .Select(p => new HeroSlideDto(p.Id, p.Name, p.ImageUrl, p.CreatedAt))
                    .ToList();
            }

            // 3. Best Sellers
            const int bestSellersLimit = 8;
            const int minBestSellersCount = 3;

            var bestSellingProducts = await ProjectToProductCardDto(
                _context.Products
                    .AsNoTracking()
                    .WhereSellable(true)
                    .Where(p => p.TotalSold > 0)
                    .OrderByDescending(p => p.TotalSold)
                    .ThenByDescending(p => p.Createdat)
                    .ThenByDescending(p => p.Id)
                    .Take(bestSellersLimit))
                .ToListAsync(cancellationToken);

            if (bestSellingProducts.Count < minBestSellersCount)
            {
                bestSellingProducts.Clear();
            }

            var showcase = new HomeShowcaseDto(newArrivals, heroSlides, bestSellingProducts);

            if (loadVersion == _invalidationService.HomeVersion)
            {
                _cache.Set(StorefrontCacheKeys.HomeShowcase, showcase, _options.HomeDuration);
                _logger.LogInformation("Populated HomeShowcase cache for {Minutes} minutes.", _options.HomeMinutes);
            }

            return showcase;
        }
        finally
        {
            HomeLock.Release();
        }
    }

    public async Task<IReadOnlyList<CategorySummaryDto>> GetCategoriesListAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(StorefrontCacheKeys.CategoriesList, out IReadOnlyList<CategorySummaryDto>? cached) && cached != null)
        {
            return cached;
        }

        await CategoriesLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(StorefrontCacheKeys.CategoriesList, out cached) && cached != null)
            {
                return cached;
            }

            var loadVersion = _invalidationService.CategoriesVersion;

            var sellableCounts = await _context.Products
                .AsNoTracking()
                .WhereSellable(available: true)
                .GroupBy(product => product.Categoryid)
                .Select(group => new { CategoryId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.CategoryId, item => item.Count, cancellationToken);

            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            var summaryList = categories.Select(c => new CategorySummaryDto(
                c.Id,
                c.Name,
                c.Description,
                c.Imageurl,
                sellableCounts.GetValueOrDefault(c.Id, 0)
            )).ToList();

            if (loadVersion == _invalidationService.CategoriesVersion)
            {
                _cache.Set(StorefrontCacheKeys.CategoriesList, summaryList, _options.CategoriesDuration);
                _logger.LogInformation("Populated CategoriesList cache for {Minutes} minutes.", _options.CategoriesMinutes);
            }

            return summaryList;
        }
        finally
        {
            CategoriesLock.Release();
        }
    }

    private static IQueryable<ProductCardDto> ProjectToProductCardDto(IQueryable<Product> query)
    {
        return query.Select(product => new ProductCardDto(
            product.Id,
            product.Categoryid,
            product.Category.Name,
            product.Name,
            product.Description,
            product.Price,
            product.Stockquantity,
            product.StockUnit,
            product.VolumeMl,
            product.IsRetailEnabled,
            product.Imageurl,
            product.Createdat,
            product.Brand,
            product.TotalSold,
            product.RetailPrices
                .Where(price => price.IsActive && price.SizeMl > 0 && price.Price > 0)
                .Select(price => new RetailPriceDto(
                    price.Id,
                    price.ProductId,
                    price.SizeMl,
                    price.Price,
                    price.IsActive
                )).ToList()
        ));
    }
}
