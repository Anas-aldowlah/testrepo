using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Models.UsersDatabase;
using YAGOT_2._0.Data;
namespace YAGOT_2._0.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Developer")]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public class DashboardController : Controller
{
    private readonly NeondbContext _context;
    private readonly UsersDbContext _dbUser;

    public DashboardController(NeondbContext context,UsersDbContext user)
    {
        _context = context;
        _dbUser = user;
    }
    public async Task<IActionResult> Index()
    {
        var model = new AdminDashboardViewModel
        {
            TotalProducts = await _context.Products.CountAsync(),
            TotalOrders = await _context.Orders.CountAsync(),
            TotalUsers = await _dbUser.Users.CountAsync(),
            TotalRevenue = await _context.Orders.WhereRevenueEligible()
                .SumAsync(o => (decimal?)(o.Finalfulfilledamount ?? o.Totalamount)) ?? 0m,
            PendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending"),
            ActiveOrders = await _context.Orders.CountAsync(o => o.Status == "Processed" || o.Status == "Shipped"),
            RecentOrders = await _context.Orders
                .AsNoTracking()
                .OrderByDescending(o => o.Orderdate)
                .Take(6)
                .ToListAsync()
        };

        return View(model);
    }
}
