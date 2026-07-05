using Microsoft.AspNetCore.Mvc;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;

namespace Yagot.Areas.Admin.Controllers;

[Area("Admin")]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public class UsersController : Controller
{
    private readonly NeondbContext _context;

    public UsersController(NeondbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        return View(_context.Users);
    }
}
