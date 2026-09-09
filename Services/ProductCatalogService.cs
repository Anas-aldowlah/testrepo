using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services.Caching;

namespace YAGOT_2._0.Services;

public sealed class ProductCatalogService
{
    public const int PageSize = 8;

    private readonly NeondbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ICacheInvalidationService _invalidationService;
    private readonly BackendCacheOptions _cacheOptions;
    private static readonly SemaphoreSlim MetadataLock = new(1, 1);
    private static readonly string MetadataCacheKey = StorefrontCacheKeys.CatalogMetadata;
    private static long _metadataVersion;

    public ProductCatalogService(
        NeondbContext context,
        IMemoryCache cache,
        ICacheInvalidationService invalidationService,
        IOptions<BackendCacheOptions> cacheOptions)
    {
        _context = context;
        _cache = cache;
        _invalidationService = invalidationService;
        _cacheOptions = cacheOptions.Value;
    }

    public async Task<ProductsCatalogViewModel> GetCatalogAsync(
        ProductsCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Search = Normalize(request.Search);

        request.Brand = (request.Brand ?? [])
            .Where(b => !string.IsNullOrWhiteSpace(b) && b != "-")
            .Select(b => b.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        request.RetailSize = (request.RetailSize ?? []).Distinct().OrderBy(size => size).ToArray();

        if (request.Retail == "no" && request.RetailSize.Length > 0)
        {
            request.RetailSize = [];
        }

        var query = _context.Products
            .AsNoTracking();

        if (request.CategoryId.HasValue && request.CategoryId != -100)
            query = query.Where(product => product.Categoryid == request.CategoryId);

        if (request.Brand.Length > 0)
        {
            var loweredBrands = request.Brand.Select(b => b.ToLower()).ToArray();
            query = query.Where(product =>
                product.Brand != null &&
                loweredBrands.Contains(product.Brand.ToLower()));
        }

        if (request.Search is not null)
        {
            var pattern = $"%{EscapeLikePattern(request.Search)}%";
            query = query.Where(product =>
                EF.Functions.ILike(product.Name, pattern, "\\") ||
                (product.Description != null && EF.Functions.ILike(product.Description, pattern, "\\")) ||
                (product.Brand != null && EF.Functions.ILike(product.Brand, pattern, "\\")));
        }

        if (request.MinPrice.HasValue)
            query = query.Where(product => product.Price >= request.MinPrice);

        if (request.MaxPrice.HasValue)
            query = query.Where(product => product.Price <= request.MaxPrice);

        if (request.Availability == "available")
            query = query.WhereSellable(available: true);
        else if (request.Availability == "unavailable")
            query = query.WhereSellable(available: false);

        if (request.Retail == "yes")
            query = query.WhereEffectiveRetailAvailability(available: true);
        else if (request.Retail == "no")
            query = query.WhereEffectiveRetailAvailability(available: false);

        if (request.RetailSize.Length > 0)
        {
            query = query.Where(product =>
                product.IsRetailEnabled &&
                product.StockUnit == "Ml" &&
                product.VolumeMl.HasValue &&
                product.VolumeMl.Value > 0 &&
                product.RetailPrices.Any(price =>
                    price.IsActive &&
                    price.SizeMl > 0 &&
                    price.Price > 0 &&
                    price.SizeMl < product.VolumeMl.Value &&
                    product.Stockquantity >= price.SizeMl &&
                    request.RetailSize.Contains(price.SizeMl)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)PageSize);
        var currentPage = Math.Min(request.Page, totalPages);
        request.Page = currentPage;

        var orderedQuery = ApplyOrdering(query, request.Sort);
        var products = await orderedQuery
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToProductCardsAsync(cancellationToken);

        var metadata = await GetMetadataAsync(cancellationToken);
        var retailSizes = await GetRetailSizesAsync(cancellationToken);

        return new ProductsCatalogViewModel
        {
            Request = request,
            Products = products,
            Categories = metadata.Categories,
            Brands = metadata.Brands,
            RetailSizes = retailSizes,
            TotalCount = totalCount,
            PageSize = PageSize,
            CurrentPage = currentPage,
            TotalPages = totalPages
        };
    }

    public void InvalidateMetadataCache()
    {
        Interlocked.Increment(ref _metadataVersion);
        _cache.Remove(MetadataCacheKey);
        _invalidationService.InvalidateCatalogMetadata();
    }

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        (await GetMetadataAsync(cancellationToken)).Categories;

    public async Task<IReadOnlyList<string>> GetBrandsAsync(CancellationToken cancellationToken = default) =>
        (await GetMetadataAsync(cancellationToken)).Brands;

    private async Task<CatalogMetadata> GetMetadataAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(MetadataCacheKey, out CatalogMetadata? cached) && cached != null)
            return cached;

        await MetadataLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(MetadataCacheKey, out cached) && cached != null)
                return cached;

            var loadVersion = Volatile.Read(ref _metadataVersion);
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(category => category.Name)
                .ThenBy(category => category.Id)
                .ToListAsync(cancellationToken);

            var brands = await _context.Products
                .AsNoTracking()
                .Where(product => product.Brand != null && product.Brand != "-")
                .Select(product => product.Brand!.Trim())
                .Distinct()
                .ToListAsync(cancellationToken);

            var retailSizes = await _context.ProductRetailPrices
                .AsNoTracking()
                .Where(price =>
                    price.IsActive && price.SizeMl > 0 && price.Price > 0 &&
                    price.Product.IsRetailEnabled && price.Product.StockUnit == "Ml" &&
                    price.Product.VolumeMl.HasValue &&
                    price.SizeMl < price.Product.VolumeMl.Value &&
                    price.Product.Stockquantity >= price.SizeMl)
                .Select(price => price.SizeMl)
                .Distinct()
                .OrderBy(size => size)
                .ToListAsync(cancellationToken);

            var metadata = new CatalogMetadata(
                categories,
                brands.Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(brand => brand, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                retailSizes);

            if (loadVersion == Volatile.Read(ref _metadataVersion))
                _cache.Set(MetadataCacheKey, metadata, _cacheOptions.CatalogDuration);

            return metadata;
        }
        finally
        {
            MetadataLock.Release();
        }
    }

    private async Task<IReadOnlyList<int>> GetRetailSizesAsync(CancellationToken cancellationToken) =>
        (await GetMetadataAsync(cancellationToken)).RetailSizes;

    private static IOrderedQueryable<Product> ApplyOrdering(IQueryable<Product> query, string sort)
    {
        return sort switch
        {
            "price-asc" => query.OrderBy(product => product.Price).ThenBy(product => product.Id),
            "price-desc" => query.OrderByDescending(product => product.Price).ThenBy(product => product.Id),
            "name" => query.OrderBy(product => product.Name).ThenBy(product => product.Id),
            _ => query.OrderByDescending(product => product.Createdat ?? DateTime.MinValue)
                .ThenByDescending(product => product.Id)
        };
    }

    private static string? Normalize(string? value)
    {
        value = value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    private sealed record CatalogMetadata(
        IReadOnlyList<Category> Categories,
        IReadOnlyList<string> Brands,
        IReadOnlyList<int> RetailSizes);
}
