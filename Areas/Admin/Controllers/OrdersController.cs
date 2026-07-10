using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
namespace YAGOT_2._0.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class OrdersController : Controller
{
    private readonly NeondbContext _context;

    private static readonly HashSet<string> ActiveStatuses = new()
    {
        "Processed",
        "Shipped",
        "Delivered"
    };

    private static readonly HashSet<string> AllowedStatuses = new()
    {
        "Pending",
        "Processed",
        "Shipped",
        "Delivered",
        "Cancelled"
    };

    public OrdersController(NeondbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string[]? status, string? search)
    {
        IQueryable<Order> query = _context.Orders
            .Include(o => o.User)
            .Include(o => o.Orderitems)
                .ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.Orderdate);

        // ????? ??? ??????
        if (status is { Length: > 0 })
        {
            query = query.Where(o => status.Contains(o.Status));
        }

        // ?????
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(o =>
                (o.User != null && o.User.Name.Contains(search)) ||
                (!string.IsNullOrEmpty(o.Trackingnumber) &&
                 o.Trackingnumber.Contains(search)));
        }

        ViewBag.SelectedStatus = status ?? Array.Empty<string>();
        ViewBag.Search = search;

        return View(await query.ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        if (!AllowedStatuses.Contains(status))
            return BadRequest("Invalid order status.");

        var order = await _context.Orders
            .Include(o => o.Orderitems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        if (order.Status == status)
            return RedirectToAction(nameof(Index));

        try
        {
            // ??? ???????
            if (order.Status == "Pending" &&
                ActiveStatuses.Contains(status))
            {
                foreach (var item in order.Orderitems)
                {
                    if (item.Product.Stockquantity < item.Quantity)
                    {
                        TempData["Error"] =
                            $"Not enough stock for {item.Product.Name}.";

                        return RedirectToAction(nameof(Index));
                    }

                    item.Product.Stockquantity -= item.Quantity;
                }
            }

            // ????? ???????
            if (status == "Cancelled" &&
                ActiveStatuses.Contains(order.Status))
            {
                foreach (var item in order.Orderitems)
                {
                    item.Product.Stockquantity += item.Quantity;
                }
            }

            order.Status = status;
            order.TimeState = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Order status updated successfully.";
        }
        catch
        {
            TempData["Error"] = "An unexpected error occurred.";
        }

        return RedirectToAction(nameof(Index));
    }
}
