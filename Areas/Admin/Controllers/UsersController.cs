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
            .Where(us => us.UserId.HasValue && userIds.Contains(us.UserId.Value))
            .ToListAsync();

        var roleCache = new Dictionary<int, string?>();
        foreach (var us in userSites)
        {
            if (us.UserId.HasValue)
            {
                roleCache[us.UserId.Value] = us.Role;
            }
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
}
