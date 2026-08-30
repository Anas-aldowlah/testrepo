using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
    private readonly StoreSettingsService _settingsService;

    private static readonly HashSet<string> AllowedStatuses =
        new(OrderStatusPolicy.DisplayStatuses, StringComparer.OrdinalIgnoreCase);

    public OrdersController(
        NeondbContext context,
        UsersDbContext dbUser,
        OrderService orderService,
        ReceiptStorageService receiptStorage,
        StoreSettingsService settingsService)
    {
        _context = context;
        _dbUser = dbUser;
        _orderService = orderService;
        _receiptStorage = receiptStorage;
        _settingsService = settingsService;
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
                .AsNoTracking()
                .Where(u => u.Name.Contains(search))
                .Select(u => u.Id)
                .Take(200)
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
        ViewData["PaymentMethodPresentations"] = await _settingsService.GetPaymentMethodPresentationsAsync(
            pagedOrders.Items.Select(order => order.Paymentmethod),
            HttpContext.RequestAborted);

        var summary = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Pending = group.Count(order => order.Status == OrderStatuses.Pending),
                Active = group.Count(order => order.Status == OrderStatuses.Paid || order.Status == OrderStatuses.Processed || order.Status == OrderStatuses.Shipped),
                Delivered = group.Count(order => order.Status == OrderStatuses.Delivered),
                Revenue = group
                    .Where(order => order.Stockdeducted &&
                        (order.Status == OrderStatuses.Paid || order.Status == OrderStatuses.Processed || order.Status == OrderStatuses.Shipped || order.Status == OrderStatuses.Delivered))
                    .Sum(order => (decimal?)(order.Finalfulfilledamount ?? order.Totalamount)) ?? 0m
            })
            .SingleOrDefaultAsync();

        var model = new AdminOrdersIndexViewModel
        {
            Orders = pagedOrders,
            SelectedStatus = selectedStatus,
            Search = search ?? string.Empty,
            PendingCount = summary?.Pending ?? 0,
            ActiveCount = summary?.Active ?? 0,
            DeliveredCount = summary?.Delivered ?? 0,
            TotalRevenue = summary?.Revenue ?? 0m
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
        ViewData["PaymentMethodPresentations"] = await _settingsService.GetPaymentMethodPresentationsAsync(
            [order.Paymentmethod],
            HttpContext.RequestAborted);
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
                return Json(new
                {
                    success = true,
                    status = normalizedStatus,
                    allowedTargets = OrderStatusPolicy.GetAllowedTargets(normalizedStatus),
                    whatsAppUrl = normalizedStatus == OrderStatuses.Delivered
                        ? await BuildWhatsAppUrlAsync(id)
                        : null,
                    message = normalizedStatus == OrderStatuses.Cancelled
                        ? "أُلغي الطلب بنجاح، ولن تتم متابعته أو تجهيزه."
                        : "تم تحديث حالة الطلب بنجاح."
                });

            TempData["Success"] = normalizedStatus == OrderStatuses.Cancelled
                ? "أُلغي الطلب بنجاح، ولن تتم متابعته أو تجهيزه."
                : "تم تحديث حالة الطلب بنجاح.";
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
    public async Task<IActionResult> VerifyPayment(int id)
    {
        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var adminIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(adminIdValue, out var adminId) || adminId <= 0)
            return Forbid();

        try
        {
            var outcome = await _orderService.VerifyPaymentAsync(id, adminId, HttpContext.RequestAborted);
            if (outcome == PaymentVerificationOutcome.NotFound)
                return NotFound();

            var message = outcome == PaymentVerificationOutcome.Paid
                ? "تم تأكيد الدفع، ويمكنك الآن متابعة تجهيز الطلب وإشعار العميل."
                : "الطلب بانتظار قرار العميل بشأن الكمية المتوفرة. لا يلزم التحقق من الدفع مرة أخرى.";
            if (isAjax)
                return Json(new
                {
                    success = true,
                    outcome = outcome.ToString(),
                    message,
                    whatsAppUrl = await BuildWhatsAppUrlAsync(id)
                });

            TempData["Success"] = message;
        }
        catch (InvalidOperationException ex)
        {
            if (isAjax)
                return Conflict(new { success = false, message = ex.Message });
            TempData["Error"] = ex.Message;
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            if (isAjax)
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "تعذر التحقق من الدفع." });
            TempData["Error"] = "حدث خطأ غير متوقع أثناء التحقق من الدفع.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<string?> BuildWhatsAppUrlAsync(int orderId)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(item => item.Orderitems)
                .ThenInclude(item => item.Product)
            .Include(item => item.Deliveryorder)
            .SingleOrDefaultAsync(item => item.Id == orderId, HttpContext.RequestAborted);
        return order == null ? null : OrderWhatsAppLinkBuilder.Build(order, null);
    }

}
