using YAGOT_2._0.Models;

namespace YAGOT_2._0.Models.Admin;

public class AdminOrdersIndexViewModel
{
    public PagedResult<Order> Orders { get; init; } = new();
    public string[] SelectedStatus { get; init; } = Array.Empty<string>();
    public string Search { get; init; } = string.Empty;
    public int PendingCount { get; init; }
    public int ActiveCount { get; init; }
    public int DeliveredCount { get; init; }
    public decimal TotalRevenue { get; init; }
    public bool HasActiveFilters => SelectedStatus.Length > 0 || !string.IsNullOrWhiteSpace(Search);
}
