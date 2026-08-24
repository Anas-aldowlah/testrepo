using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Data;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services;
namespace YAGOT_2._0.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Developer")]
public class OrdersController : Controller
{
    private readonly NeondbContext _context;
    private readonly UsersDbContext _dbUser;
    private readonly OrderService _orderService;
    private readonly ReceiptStorageService _receiptStorage;

    private static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Pending",
            "Processed",
            "Shipped",
            "Delivered",
            "Cancelled",
            "Refunded"
        };

    public OrdersController(
        NeondbContext context,
        UsersDbContext dbUser,
        OrderService orderService,
        ReceiptStorageService receiptStorage)
    {
        _context = context;
        _dbUser = dbUser;
        _orderService = orderService;
        _receiptStorage = receiptStorage;
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

        if (!string.IsNullOrWhiteSpace(search))
        {
            if (searchUserIds.Count > 0)
            {
                query = query.Where(o =>
                    searchUserIds.Contains(o.Userid) ||
                    (!string.IsNullOrWhiteSpace(o.Trackingnumber) && o.Trackingnumber.Contains(search)));
            }
            else
            {
                query = query.Where(o =>
                    !string.IsNullOrWhiteSpace(o.Trackingnumber) &&
                    o.Trackingnumber.Contains(search));
            }
        }

        var pageQuery = query
            .Include(o => o.Orderitems)
                .ThenInclude(oi => oi.Product)
            .Include(o => o.Deliveryorder)
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
            .Include(o => o.Deliveryorder)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        await PopulateUserDisplayDataAsync(new[] { order });
        return View(order);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Developer")]
    public async Task<IActionResult> Receipt(int id)
    {
        var storedValue = await _context.Orders
            .AsNoTracking()
            .Where(order => order.Id == id)
            .Select(order => order.Receipturl)
            .SingleOrDefaultAsync(HttpContext.RequestAborted);

        if (string.IsNullOrWhiteSpace(storedValue) ||
            !_receiptStorage.TryOpen(storedValue, out var stream, out var contentType) ||
            stream == null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.Pragma = "no-cache";
        return File(stream, contentType, enableRangeProcessing: true);
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
        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var normalizedStatus = OrderService.NormalizeStatus(status);
        if (normalizedStatus == null || !AllowedStatuses.Contains(normalizedStatus))
        {
            if (isAjax)
                return Json(new { success = false, message = "حالة الطلب غير صالحة." });
            return BadRequest("Invalid order status.");
        }

        try
        {
            if (!await _orderService.UpdateStatusAsync(id, normalizedStatus))
            {
                if (isAjax)
                    return Json(new { success = false, message = "الطلب غير موجود." });
                return NotFound();
            }

            if (isAjax)
                return Json(new { success = true, message = $"تم تحديث حالة الطلب #{id} بنجاح." });

            TempData["Success"] = "Order status updated successfully.";
        }
        catch (InvalidOperationException ex)
        {
            if (isAjax)
                return Json(new { success = false, message = ex.Message });
            TempData["Error"] = ex.Message;
        }
        catch
        {
            if (isAjax)
                return Json(new { success = false, message = "حدث خطأ غير متوقع أثناء تحديث الطلب." });
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePaymentStatus(int id, string paymentStatus, bool returnToDetails = false)
    {
        var allowed = new[] { "Unpaid", "Pending", "Paid", "Refunded" };
        if (!allowed.Contains(paymentStatus))
        {
            TempData["Error"] = "حالة الدفع غير صالحة.";
            return returnToDetails 
                ? RedirectToAction(nameof(Details), new { id })
                : RedirectToAction(nameof(Index));
        }

        try
        {
            // Paid and Refunded both pass through the same Serializable transaction,
            // product row locks, and Stockdeducted checks as order status updates.
            if (!await _orderService.UpdatePaymentStatusAsync(id, paymentStatus))
                return NotFound();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    success = true,
                    message = $"تم تحديث حالة الدفع للطلب #{id} بنجاح.",
                    status = paymentStatus
                });
            }

            TempData["Success"] = paymentStatus == "Refunded"
                ? $"تم استرداد الطلب #{id} وإعادة مخزونه بنجاح."
                : $"تم تحديث حالة الدفع للطلب #{id} بنجاح.";
        }
        catch (InvalidOperationException ex)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = false, message = ex.Message });
            TempData["Error"] = ex.Message;
        }
        catch
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = false, message = "حدث خطأ غير متوقع أثناء تحديث حالة الدفع." });
            TempData["Error"] = "An unexpected error occurred while updating payment status.";
        }

        return returnToDetails 
            ? RedirectToAction(nameof(Details), new { id })
            : RedirectToAction(nameof(Index));
    }
}
