using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Data;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services;
using static YAGOT_2._0.Services.DealingAPI;
namespace Yagot.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Developer")]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public class UsersController : Controller
{
    private readonly NeondbContext _context;
    private readonly DealingAPI _DealingAPI;
    private readonly UsersDbContext _dbUser;
    public UsersController(NeondbContext context,DealingAPI dealingAPI,UsersDbContext User)
    {
        _context = context;
        _DealingAPI = dealingAPI;
        _dbUser = User;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
    {
        var usersPage = await PagedResult<YAGOT_2._0.Models.UsersDatabase.User>.CreateAsync(
            _dbUser.Users
            .AsNoTracking()
            .OrderBy(user => user.Id),
            page,
            pageSize);

        var userIds = usersPage.Items.Select(user => user.Id).ToArray();

        var userSites = await _context.UserSites
            .AsNoTracking()
            .Where(us => userIds.Contains(us.UserId))
            .ToListAsync();

        var roleCache = new Dictionary<int, string?>();
        foreach (var us in userSites)
        {
            roleCache[us.UserId] = us.Role;
        }

        foreach (var user in usersPage.Items)
        {
            user.Phone = DecryptPhoneOrUnavailable(user.Phone);
            user.Role = roleCache.GetValueOrDefault(user.Id);
        }

        var model = new AdminUsersIndexViewModel
        {
            Users = usersPage,
            TotalUsers = await _dbUser.Users.CountAsync()
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await _dbUser.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        user.Phone = DecryptPhoneOrUnavailable(user.Phone);
        user.Role = await _context.UserSites
            .Where(us => us.UserId == id)
            .Select(us => us.Role)
            .FirstOrDefaultAsync();

        ViewBag.RecentOrders = await _context.Orders
            .AsNoTracking()
            .Where(order => order.Userid == id)
            .OrderByDescending(order => order.Orderdate)
            .Take(5)
            .ToListAsync();

        return View(user);
    }

    private string DecryptPhoneOrUnavailable(string? encryptedPhone)
    {
        if (string.IsNullOrWhiteSpace(encryptedPhone))
        {
            return string.Empty;
        }

        try
        {
            return _DealingAPI.DecryptPhone(encryptedPhone);
        }
        catch
        {
            return string.Empty;
        }
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Developer")]
    public async Task<IActionResult> ChangeRole(int userId, string newRole)
    {
        var userExists = await _dbUser.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            return NotFound("المستخدم غير موجود");
        }

        var allowedRoles = new[] { "Customer", "Admin", "Developer" };
        if (!allowedRoles.Contains(newRole))
        {
            return BadRequest("الدور المحدد غير صالح");
        }

        bool isCurrentUserDeveloper = User.IsInRole("Developer");

        // 1. حظر المدير من إعطاء دور Developer
        if (!isCurrentUserDeveloper && newRole == "Developer")
        {
            TempData["ErrorMessage"] = "عذراً، لا تملك الصلاحية لترقية الحساب إلى دور مطور.";
            return RedirectToAction(nameof(Details), new { id = userId });
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var userSite = await _context.UserSites
                .FirstOrDefaultAsync(us => us.UserId == userId);

            // 2. حظر المدير من تعديل دور شخص هو بالأساس Developer
            if (!isCurrentUserDeveloper && userSite != null && userSite.Role == "Developer")
            {
                TempData["ErrorMessage"] = "عذراً، لا يمكنك تعديل صلاحيات حسابات المطورين.";
                return RedirectToAction(nameof(Details), new { id = userId });
            }

            if (string.Equals(userSite?.Role, newRole, StringComparison.Ordinal))
            {
                return RedirectToAction(nameof(Details), new { id = userId });
            }

            // 3. منع فقدان آخر حساب بصلاحيات عليا (SM-17 & BM-17)
            if (userSite != null && (userSite.Role == "Developer" || userSite.Role == "Admin"))
            {
                var devCount = await _context.UserSites.CountAsync(us => us.Role == "Developer");
                var adminCount = await _context.UserSites.CountAsync(us => us.Role == "Admin");

                if (userSite.Role == "Developer" && devCount <= 1)
                {
                    TempData["ErrorMessage"] = "لا يمكن تجريد المطور الوحيد في النظام من صلاحياته.";
                    return RedirectToAction(nameof(Details), new { id = userId });
                }

                if (userSite.Role == "Admin" && devCount == 0 && adminCount <= 1)
                {
                    TempData["ErrorMessage"] = "لا يمكن تجريد المدير الوحيد في النظام من صلاحياته في غياب أي مطور.";
                    return RedirectToAction(nameof(Details), new { id = userId });
                }
            }

            // إتمام التعديل
            if (userSite != null)
            {
                userSite.Role = newRole;
                _context.UserSites.Update(userSite);
            }
            else
            {
                var newUserSite = new YAGOT_2._0.Models.UserSite
                {
                    UserId = userId,
                    Role = newRole
                };
                await _context.UserSites.AddAsync(newUserSite);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = "تم تحديث دور المستخدم بنجاح.";
            return RedirectToAction(nameof(Details), new { id = userId });
        });
    }
}
