using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public sealed class ProductCatalogService
{
    public const int PageSize = 12;

    private readonly NeondbContext _context;

    public ProductCatalogService(NeondbContext context)
    {
        _context = context;
    }

    public async Task<ProductsCatalogViewModel> GetCatalogAsync(
        ProductsCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Search = Normalize(request.Search);
        request.Brand = Normalize(request.Brand);
        if (request.Brand == "-")
            request.Brand = null;

        request.RetailSize = (request.RetailSize ?? []).Distinct().OrderBy(size => size).ToArray();

        var query = _context.Products
            .AsNoTracking()
            .Where(product => product.Stockquantity > 0);

        if (request.CategoryId.HasValue && request.CategoryId != -100)
            query = query.Where(product => product.Categoryid == request.CategoryId);

        if (request.Brand is not null)
        {
            var brandPattern = EscapeLikePattern(request.Brand);
            query = query.Where(product =>
                product.Brand != null && EF.Functions.ILike(product.Brand, brandPattern, "\\"));
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

        if (request.Retail == "yes")
            query = query.Where(product => product.IsRetailEnabled);
        else if (request.Retail == "no")
            query = query.Where(product => !product.IsRetailEnabled);

        if (request.RetailSize.Length > 0)
        {
            query = query.Where(product =>
                product.IsRetailEnabled &&
                product.RetailPrices.Any(price =>
                    price.IsActive && request.RetailSize.Contains(price.SizeMl)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)PageSize);
        var currentPage = Math.Min(request.Page, totalPages);
        request.Page = currentPage;

        var orderedQuery = ApplyOrdering(query, request.Sort);
        var products = await orderedQuery
            .Include(product => product.Category)
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .ToListAsync(cancellationToken);

        var brandRows = await _context.Products
            .AsNoTracking()
            .Where(product => product.Stockquantity > 0 && product.Brand != null && product.Brand != "-")
            .Select(product => product.Brand!.Trim())
            .Distinct()
            .ToListAsync(cancellationToken);
        var brands = brandRows
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(brand => brand, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var retailSizes = await _context.ProductRetailPrices
            .AsNoTracking()
            .Where(price =>
                price.IsActive &&
                price.SizeMl > 0 &&
                price.Product.IsRetailEnabled &&
                price.Product.Stockquantity > 0)
            .Select(price => price.SizeMl)
            .Distinct()
            .OrderBy(size => size)
            .ToListAsync(cancellationToken);

        return new ProductsCatalogViewModel
        {
            Request = request,
            Products = products,
            Categories = categories,
            Brands = brands,
            RetailSizes = retailSizes,
            TotalCount = totalCount,
            PageSize = PageSize,
            CurrentPage = currentPage,
            TotalPages = totalPages
        };
    }

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
}
