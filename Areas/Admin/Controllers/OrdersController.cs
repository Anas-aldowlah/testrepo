using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
namespace YAGOT_2._0.Areas.Admin.Controllers;

[Area("Admin")]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public class OrdersController : Controller
{
    private readonly NeondbContext _context;
    public OrdersController(NeondbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var orders = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Orderitems)
                .ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.Orderdate)
            .ToListAsync();

        return View(orders);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        DateTime now = DateTime.Now;
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order != null)
        {
            order.Status = status;
            order.TimeState = now;
        }
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
