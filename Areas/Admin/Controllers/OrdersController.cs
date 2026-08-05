using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Data;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
namespace YAGOT_2._0.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class OrdersController : Controller
{
    private readonly NeondbContext _context;
    private readonly UsersDbContext _dbUser;

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

    public OrdersController(NeondbContext context, UsersDbContext dbUser)
    {
        _context = context;
        _dbUser = dbUser;
    }

    public async Task<IActionResult> Index(string[]? status, string? search, int page = 1, int pageSize = 10)
    {
        var selectedStatus = status?
            .Where(s => !string.IsNullOrWhiteSpace(s) && AllowedStatuses.Contains(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        IQueryable<Order> query = _context.Orders
            .AsNoTracking();

        var searchUserIds = new HashSet<int>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            var matchedUserIds = await _dbUser.Users
                .Where(u => u.Name.Contains(search))
                .Select(u => u.Id)
                .ToListAsync();

            searchUserIds = new HashSet<int>(matchedUserIds);
        }

        if (selectedStatus.Any())
        {
            query = query.Where(o => selectedStatus.Contains(o.Status));
        }

        if (searchUserIds.Count > 0)
        {
            query = query.Where(o => searchUserIds.Contains(o.Userid));
        }
        else if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(o =>
                !string.IsNullOrWhiteSpace(o.Trackingnumber) &&
                o.Trackingnumber.Contains(search));
        }

        var pageQuery = query
            .Include(o => o.Orderitems)
                .ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.Orderdate);

        var pagedOrders = await PagedResult<Order>.CreateAsync(pageQuery, page, pageSize);
        await PopulateUserDisplayDataAsync(pagedOrders.Items);

        var model = new AdminOrdersIndexViewModel
        {
            Orders = pagedOrders,
            SelectedStatus = selectedStatus,
            Search = search ?? string.Empty,
            PendingCount = await query.CountAsync(o => o.Status == "Pending"),
            ActiveCount = await query.CountAsync(o => o.Status == "Processed" || o.Status == "Shipped"),
            DeliveredCount = await query.CountAsync(o => o.Status == "Delivered"),
            TotalRevenue = await query.SumAsync(o => (decimal?)o.Totalamount) ?? 0m
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Orderitems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        await PopulateUserDisplayDataAsync(new[] { order });
        return View(order);
    }

    private async Task PopulateUserDisplayDataAsync(IEnumerable<Order> orders)
    {
        var userIds = orders.Select(o => o.Userid).Distinct().ToList();
        var users = await _dbUser.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();

        var userCache = users.ToDictionary(u => u.Id);

        foreach (var order in orders)
        {
            if (userCache.TryGetValue(order.Userid, out var user))
            {
                order.User = new UserSite
                {
                    UserId = order.Userid,
                    Name = user.Name,
                    Phone = user.Phone,
                    Email = user.Email,
                    User = user
                };
            }
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(
        int id,
        string status,
        bool returnToDetails = false,
        string[]? filterStatus = null,
        string? search = null,
        int page = 1,
        int pageSize = 10)
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
            return RedirectAfterStatusUpdate(id, returnToDetails, filterStatus, search, page, pageSize);

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
                    return RedirectAfterStatusUpdate(id, returnToDetails, filterStatus, search, page, pageSize);
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

        return RedirectAfterStatusUpdate(id, returnToDetails, filterStatus, search, page, pageSize);
    }

    private IActionResult RedirectAfterStatusUpdate(
        int orderId,
        bool returnToDetails,
        string[]? filterStatus = null,
        string? search = null,
        int page = 1,
        int pageSize = 10)
    {
        if (returnToDetails)
            return RedirectToAction(nameof(Details), new { id = orderId });

        return RedirectToAction(nameof(Index), new
        {
            status = filterStatus,
            search,
            page,
            pageSize
        });
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePaymentStatus(int id, string paymentStatus, bool returnToDetails = false)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();

        var allowed = new[] { "Unpaid", "Pending", "Paid", "Refunded" };
        if (!allowed.Contains(paymentStatus))
        {
            TempData["Error"] = "حالة الدفع غير صالحة.";
            return returnToDetails 
                ? RedirectToAction(nameof(Details), new { id })
                : RedirectToAction(nameof(Index));
        }

        order.Paymentstatus = paymentStatus;
        await _context.SaveChangesAsync();
        TempData["Success"] = $"تم تحديث حالة الدفع للطلب #{id} بنجاح.";

        return returnToDetails 
            ? RedirectToAction(nameof(Details), new { id })
            : RedirectToAction(nameof(Index));
    }
    private Task<(bool isValid, string errorMessage, string detectedExtension)> IsValidImageFileAsync(IFormFile file)
{
    if (file == null || file.Length == 0)
        return Task.FromResult((false, "لم يتم رفع أي صورة.", string.Empty));

    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (!allowedExtensions.Contains(extension))
        return Task.FromResult((false, "صيغة الملف غير مدعومة.", extension));

    return Task.FromResult((true, string.Empty, extension));
}
}
