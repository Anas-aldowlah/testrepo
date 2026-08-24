using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
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
        var categoriesWithCounts = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                Category = c,
                InStockCount = c.Products.Count(p => p.Stockquantity > 0)
            })
            .ToListAsync();

        var categories = categoriesWithCounts.Select(x =>
        {
            var cat = x.Category;
            cat.Products = Enumerable.Range(0, x.InStockCount)
                .Select(_ => new Product { Stockquantity = 1 })
                .ToList();
            return cat;
        }).ToList();

        return View(categories);
    }
}