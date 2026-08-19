using System.ComponentModel.DataAnnotations;

namespace YAGOT_2._0.Models;

public sealed class ProductsCatalogRequest : IValidatableObject
{
    public int? CategoryId { get; set; }

    [StringLength(200)]
    public string? Search { get; set; }

    [StringLength(150)]
    public string? Brand { get; set; }

    [Range(typeof(decimal), "0", "99999999.99")]
    public decimal? MinPrice { get; set; }

    [Range(typeof(decimal), "0", "99999999.99")]
    public decimal? MaxPrice { get; set; }

    public string Retail { get; set; } = "all";

    public int[] RetailSize { get; set; } = [];

    public string Sort { get; set; } = "newest";

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinPrice.HasValue && MaxPrice.HasValue && MinPrice > MaxPrice)
        {
            yield return new ValidationResult(
                "Minimum price cannot exceed maximum price.",
                [nameof(MinPrice), nameof(MaxPrice)]);
        }

        if (Retail is not ("all" or "yes" or "no"))
            yield return new ValidationResult("Unsupported retail filter.", [nameof(Retail)]);

        if (Sort is not ("newest" or "price-asc" or "price-desc" or "name"))
            yield return new ValidationResult("Unsupported catalog sort.", [nameof(Sort)]);

        if (RetailSize?.Any(size => size <= 0) == true)
            yield return new ValidationResult("Retail size must be greater than zero.", [nameof(RetailSize)]);
    }
}

public sealed class ProductsCatalogViewModel
{
    public ProductsCatalogRequest Request { get; init; } = new();
    public IReadOnlyList<Product> Products { get; init; } = [];
    public IReadOnlyList<Category> Categories { get; init; } = [];
    public IReadOnlyList<string> Brands { get; init; } = [];
    public IReadOnlyList<int> RetailSizes { get; init; } = [];
    public int TotalCount { get; init; }
    public int PageSize { get; init; }
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }
    public bool HasNextPage => CurrentPage < TotalPages;
    public int RemainingCount => Math.Max(0, TotalCount - (CurrentPage * PageSize));
    public Category? SelectedCategory => Categories.FirstOrDefault(category => category.Id == Request.CategoryId);
}
