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

    private static readonly HashSet<string> ActiveStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Processed",
            "Shipped",
            "Delivered"
        };

    private static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.OrdinalIgnoreCase)
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
            .AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.Orderitems)
                .ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.Orderdate);

        // ????? ??? ??????
        if (status?.Any() == true)
        {
            query = query.Where(o => status.Contains(o.Status));
        }

        // ?????
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(o =>
                (o.User != null && o.User.Name.Contains(search)) ||
                (!string.IsNullOrWhiteSpace(o.Trackingnumber) &&
                 o.Trackingnumber.Contains(search)));
        }

        ViewBag.SelectedStatus = status ?? Array.Empty<string>();
        ViewBag.Search = search;

        return View(await query.ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.Orderitems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status, bool returnToDetails = false)
    {
        if (string.IsNullOrWhiteSpace(status))
            return BadRequest();

        status = status.Trim();

        if (!AllowedStatuses.Contains(status))
            return BadRequest("Invalid order status.");

        var order = await _context.Orders
            .Include(o => o.Orderitems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        // ?? ??? ??? ??? ??? ???? ?????? ?????
        if (order.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            return RedirectAfterStatusUpdate(id, returnToDetails);

        bool wasActive = ActiveStatuses.Contains(order.Status);
        bool willBeActive = ActiveStatuses.Contains(status);

        try
        {
            // ???????? ?? ???? ??? ????? ??? ???? ????? => ??? ???????
            if (!wasActive && willBeActive)
            {
                if (!DeductStock(order))
                {
                    TempData["Error"] = "One or more products do not have sufficient stock.";
                    return RedirectAfterStatusUpdate(id, returnToDetails);
                }
            }

            // ???????? ?? ???? ????? ??? ???? ??? ????? => ????? ???????
            if (wasActive && !willBeActive)
            {
                RestoreStock(order);
            }

            order.Status = status;
            order.TimeState = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Order status updated successfully.";
        }
        catch
        {
            TempData["Error"] = "An unexpected error occurred while updating the order.";
        }

        return RedirectAfterStatusUpdate(id, returnToDetails);
    }

    private IActionResult RedirectAfterStatusUpdate(int orderId, bool returnToDetails)
    {
        if (returnToDetails)
            return RedirectToAction(nameof(Details), new { id = orderId });

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// ??? ???? ????????.
    /// </summary>
    private bool DeductStock(Order order)
    {
        foreach (var item in order.Orderitems)
        {
            if (item.Product == null)
                return false;

            if (item.Product.Stockquantity < item.Quantity)
                return false;
        }

        foreach (var item in order.Orderitems)
        {
            item.Product!.Stockquantity -= item.Quantity;
        }

        return true;
    }

    /// <summary>
    /// ????? ???? ???????? ???????.
    /// </summary>
    private void RestoreStock(Order order)
    {
        foreach (var item in order.Orderitems)
        {
            if (item.Product != null)
            {
                item.Product.Stockquantity += item.Quantity;
            }
        }
    }
}
