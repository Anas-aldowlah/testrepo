using YAGOT_2._0.Models;

namespace YAGOT_2._0.Models.Admin;

public class AdminCategoriesIndexViewModel
{
    public PagedResult<AdminCategoryListItemViewModel> Categories { get; init; } = new();
    public int TotalCategories { get; init; }
    public int TotalProducts { get; init; }
    public int CategoriesWithImages { get; init; }
}

public class AdminCategoryListItemViewModel
{
    public Category Category { get; init; } = new();
    public int ProductCount { get; init; }
}
