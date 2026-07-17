using Microsoft.AspNetCore.Mvc;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.UsersDatabase;
using YAGOT_2._0.Data;
namespace YAGOT_2._0.Areas.Admin.Controllers;

[Area("Admin")]
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
    public IActionResult Index()
    {
        ViewBag.TotalProducts = _context.Products.Count();
        ViewBag.TotalOrders = _context.Orders.Count();
        ViewBag.TotalUsers = _dbUser.Users.Count();
        ViewBag.TotalRevenue = _context.Orders.Sum(o => o.Totalamount);
        return View(_context.Orders.ToList());
    }
}
