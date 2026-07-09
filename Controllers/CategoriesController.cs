using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Controllers;

public class CategoriesController : Controller
{
    private readonly NeondbContext _context;

    public CategoriesController(NeondbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await _context.Categories
            .Include(c => c.Products)
            .ToListAsync();

        return View(categories);
    }
}