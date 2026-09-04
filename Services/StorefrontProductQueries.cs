using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

internal static class StorefrontProductQueries
{
    public static async Task<List<Product>> ToProductCardsAsync(
        this IQueryable<Product> query,
        CancellationToken cancellationToken = default)
    {
        var rows = await query
            .AsNoTracking()
            .Select(product => new ProductCardRow
            {
                Id = product.Id,
                CategoryId = product.Categoryid,
                CategoryName = product.Category.Name,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                StockQuantity = product.Stockquantity,
                StockUnit = product.StockUnit,
                VolumeMl = product.VolumeMl,
                IsRetailEnabled = product.IsRetailEnabled,
                ImageUrl = product.Imageurl,
                CreatedAt = product.Createdat,
                Brand = product.Brand,
                TotalSold = product.TotalSold,
                RetailPrices = product.RetailPrices
                    .Where(price => price.IsActive && price.SizeMl > 0 && price.Price > 0)
                    .Select(price => new RetailPriceRow
                    {
                        Id = price.Id,
                        ProductId = price.ProductId,
                        SizeMl = price.SizeMl,
                        Price = price.Price,
                        IsActive = price.IsActive
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return rows.Select(ToProduct).ToList();
    }

    public static Task<List<Product>> ToHeroProductsAsync(
        this IQueryable<Product> query,
        CancellationToken cancellationToken = default) =>
        query.AsNoTracking()
            .Select(product => new Product
            {
                Id = product.Id,
                Name = product.Name,
                Imageurl = product.Imageurl,
                Createdat = product.Createdat
            })
            .ToListAsync(cancellationToken);

    private static Product ToProduct(ProductCardRow row) => new()
    {
        Id = row.Id,
        Categoryid = row.CategoryId,
        Name = row.Name,
        Description = row.Description,
        Price = row.Price,
        Stockquantity = row.StockQuantity,
        StockUnit = row.StockUnit,
        VolumeMl = row.VolumeMl,
        IsRetailEnabled = row.IsRetailEnabled,
        Imageurl = row.ImageUrl,
        Createdat = row.CreatedAt,
        Brand = row.Brand,
        TotalSold = row.TotalSold,
        Category = new Category { Id = row.CategoryId, Name = row.CategoryName },
        RetailPrices = row.RetailPrices.Select(price => new ProductRetailPrice
        {
            Id = price.Id,
            ProductId = price.ProductId,
            SizeMl = price.SizeMl,
            Price = price.Price,
            IsActive = price.IsActive
        }).ToList()
    };

    private sealed class ProductCardRow
    {
        public int Id { get; init; }
        public int CategoryId { get; init; }
        public string CategoryName { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public decimal Price { get; init; }
        public int StockQuantity { get; init; }
        public string StockUnit { get; init; } = "Piece";
        public int? VolumeMl { get; init; }
        public bool IsRetailEnabled { get; init; }
        public string? ImageUrl { get; init; }
        public DateTime? CreatedAt { get; init; }
        public string? Brand { get; init; }
        public int TotalSold { get; init; }
        public List<RetailPriceRow> RetailPrices { get; init; } = [];
    }

    private sealed class RetailPriceRow
    {
        public int Id { get; init; }
        public int ProductId { get; init; }
        public int SizeMl { get; init; }
        public decimal Price { get; init; }
        public bool IsActive { get; init; }
    }
}
