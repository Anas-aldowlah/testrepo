using YAGOT_2._0.Models;

namespace YAGOT_2._0.Models.Admin;

public class AdminDashboardViewModel
{
    public int TotalProducts { get; init; }
    public int TotalOrders { get; init; }
    public int TotalUsers { get; init; }
    public decimal TotalRevenue { get; init; }
    public int PendingOrders { get; init; }
    public int ActiveOrders { get; init; }
    public IReadOnlyList<Order> RecentOrders { get; init; } = Array.Empty<Order>();
}
