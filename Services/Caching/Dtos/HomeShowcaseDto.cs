using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services.Caching.Dtos;

public sealed record RetailPriceDto(
    int Id,
    int ProductId,
    int SizeMl,
    decimal Price,
    bool IsActive);

public sealed record ProductCardDto(
    int Id,
    int CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    string StockUnit,
    int? VolumeMl,
    bool IsRetailEnabled,
    string? ImageUrl,
    DateTime? CreatedAt,
    string? Brand,
    int TotalSold,
    IReadOnlyList<RetailPriceDto> RetailPrices)
{
    public Product ToProduct() => new()
    {
        Id = Id,
        Categoryid = CategoryId,
        Name = Name,
        Description = Description,
        Price = Price,
        Stockquantity = StockQuantity,
        StockUnit = StockUnit,
        VolumeMl = VolumeMl,
        IsRetailEnabled = IsRetailEnabled,
        Imageurl = ImageUrl,
        Createdat = CreatedAt,
        Brand = Brand,
        TotalSold = TotalSold,
        Category = new Category { Id = CategoryId, Name = CategoryName },
        RetailPrices = RetailPrices.Select(price => new ProductRetailPrice
        {
            Id = price.Id,
            ProductId = price.ProductId,
            SizeMl = price.SizeMl,
            Price = price.Price,
            IsActive = price.IsActive
        }).ToList()
    };
}

public sealed record HeroSlideDto(
    int Id,
    string Name,
    string? ImageUrl,
    DateTime? CreatedAt)
{
    public Product ToProduct() => new()
    {
        Id = Id,
        Name = Name,
        Imageurl = ImageUrl,
        Createdat = CreatedAt
    };
}

public sealed record HomeShowcaseDto(
    IReadOnlyList<ProductCardDto> NewArrivals,
    IReadOnlyList<HeroSlideDto> HeroSlides,
    IReadOnlyList<ProductCardDto> BestSellingProducts)
{
    public List<Product> GetNewArrivalProducts() => NewArrivals.Select(p => p.ToProduct()).ToList();
    public List<Product> GetHeroSlideProducts() => HeroSlides.Select(s => s.ToProduct()).ToList();
    public List<Product> GetBestSellingProducts() => BestSellingProducts.Select(p => p.ToProduct()).ToList();
}
