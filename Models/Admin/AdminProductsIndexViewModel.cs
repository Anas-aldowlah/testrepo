using YAGOT_2._0.Models;

namespace YAGOT_2._0.Models.Admin;

public class AdminProductsIndexViewModel
{
    public PagedResult<Product> Products { get; init; } = new();
    public IReadOnlyList<Category> Categories { get; init; } = Array.Empty<Category>();
    public int TotalActiveProducts { get; init; }
    public int TotalArchivedProducts { get; init; }
    public int LowStockCount { get; init; }
    public int OutOfStockCount { get; init; }
    public string Search { get; init; } = string.Empty;
    public DateTime? BestSellersLastUpdated { get; init; }
}
