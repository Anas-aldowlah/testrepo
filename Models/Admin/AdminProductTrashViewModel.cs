using YAGOT_2._0.Models;

namespace YAGOT_2._0.Models.Admin;

public class AdminProductTrashViewModel
{
    public PagedResult<Product> Products { get; init; } = new();
    public IReadOnlyList<Category> Categories { get; init; } = Array.Empty<Category>();
    public int TotalArchivedProducts { get; init; }
}
