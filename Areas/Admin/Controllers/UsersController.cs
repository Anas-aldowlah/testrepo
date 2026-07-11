using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;
using static YAGOT_2._0.Services.DealingAPI;
namespace Yagot.Areas.Admin.Controllers;

[Area("Admin")]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public class UsersController : Controller
{
    private readonly NeondbContext _context;
    private readonly DealingAPI _DealingAPI;
    public UsersController(NeondbContext context,DealingAPI dealingAPI)
    {
        _context = context;
        _DealingAPI = dealingAPI;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .ToListAsync();

        foreach (var user in users)
        {
            user.Phone = DecryptPhoneOrUnavailable(user.Phone);
        }

        return View(users);
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        user.Phone = DecryptPhoneOrUnavailable(user.Phone);

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
