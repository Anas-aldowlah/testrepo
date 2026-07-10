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
        var users = await _context.Users.ToListAsync();

        foreach (var user in users)
        {
            user.Phone = _DealingAPI.DecryptPhone(user.Phone);
        }
        return View(_context.Users);
    }
}
