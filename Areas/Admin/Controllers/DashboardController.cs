using Microsoft.AspNetCore.Mvc;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
namespace YAGOT_2._0.Areas.Admin.Controllers;

[Area("Admin")]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public class DashboardController : Controller
{
    private readonly NeondbContext _context;

    public DashboardController(NeondbContext context)
    {
        _context = context;
    }
    public IActionResult Index()
    {
        ViewBag.TotalProducts = _context.Products.Count();
        ViewBag.TotalOrders = _context.Orders.Count();
        ViewBag.TotalUsers = _context.Users.Count();
        ViewBag.TotalRevenue = _context.Orders.Sum(o => o.Totalamount);
        return View(_context.Orders.ToList());
    }
}
