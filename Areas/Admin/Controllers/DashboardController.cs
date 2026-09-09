using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Models.UsersDatabase;
using YAGOT_2._0.Data;
using YAGOT_2._0.Services;
namespace YAGOT_2._0.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Developer")]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public class DashboardController : Controller
{
    private readonly NeondbContext _context;
    private readonly UsersDbContext _dbUser;
    private readonly MigrationStateTracker _migrationTracker;
    private readonly DatabaseMigrationCoordinator _migrationCoordinator;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        NeondbContext context,
        UsersDbContext user,
        MigrationStateTracker migrationTracker,
        DatabaseMigrationCoordinator migrationCoordinator,
        ILogger<DashboardController> logger)
    {
        _context = context;
        _dbUser = user;
        _migrationTracker = migrationTracker;
        _migrationCoordinator = migrationCoordinator;
        _logger = logger;
    }
    public async Task<IActionResult> Index()
    {
        var orderSummary = await _context.Orders
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Revenue = group
                    .Where(order => order.Stockdeducted &&
                        (order.Status == OrderStatuses.Paid ||
                         order.Status == OrderStatuses.Processed ||
                         order.Status == OrderStatuses.Shipped ||
                         order.Status == OrderStatuses.Delivered))
                    .Sum(order => (decimal?)(order.Finalfulfilledamount ?? order.Totalamount)) ?? 0m,
                Pending = group.Count(order => order.Status == OrderStatuses.Pending),
                Paid = group.Count(order => order.Status == OrderStatuses.Paid),
                Processed = group.Count(order => order.Status == OrderStatuses.Processed),
                Shipped = group.Count(order => order.Status == OrderStatuses.Shipped),
                Cancelled = group.Count(order => order.Status == OrderStatuses.Cancelled)
            })
            .SingleOrDefaultAsync();

        var model = new AdminDashboardViewModel
        {
            TotalProducts = await _context.Products.CountAsync(),
            TotalOrders = orderSummary?.Total ?? 0,
            TotalUsers = await _context.UserSites.CountAsync(),
            TotalRevenue = orderSummary?.Revenue ?? 0m,
            PendingOrders = orderSummary?.Pending ?? 0,
            PaidOrders = orderSummary?.Paid ?? 0,
            ActiveOrders = orderSummary?.Processed ?? 0,
            ShippedOrder = orderSummary?.Shipped ?? 0,
            CancelledOrder = orderSummary?.Cancelled ?? 0,
            RecentOrders = await _context.Orders
                .AsNoTracking()
                .OrderByDescending(o => o.Orderdate)
                .Take(6)
                .ToListAsync(),
            MigrationStatus = _migrationTracker.GetSnapshot()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TriggerManualSync()
    {
        var (success, errorMessage) = await _migrationCoordinator.ExecuteControlledMigrationAsync();
        if (success)
        {
            TempData["MigrationNotice"] = "تمت ترقية ومزامنة جميع قواعد البيانات بنجاح تحت حماية القفل الموزع.";
            TempData["MigrationNoticeType"] = "success";
        }
        else
        {
            _logger.LogError("Admin Dashboard: فشلت عملية الترقية اليدوية لقواعد البيانات: {Error}", errorMessage);

            if (errorMessage != null && errorMessage.Contains("قيد التنفيذ"))
            {
                TempData["MigrationNotice"] = "عملية الترقية قيد التنفيذ حالياً بواسطة مشغل أو خادم آخر. يرجى الانتظار.";
                TempData["MigrationNoticeType"] = "warning";
            }
            else
            {
                TempData["MigrationNotice"] = "تعذرت مزامنة قواعد البيانات. يرجى مراجعة سجلات الخادم للمزيد من التفاصيل.";
                TempData["MigrationNoticeType"] = "danger";
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
