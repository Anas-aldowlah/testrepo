using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services.Caching.Dtos;

public sealed record CategorySummaryDto(
    int Id,
    string Name,
    string? Description,
    string? ImageUrl,
    int SellableProductsCount)
{
    public string? Imageurl => ImageUrl;

    public Category ToCategory() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        Imageurl = ImageUrl
    };
}
