using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

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
        var sellableCounts = await _context.Products
            .AsNoTracking()
            .WhereSellable(available: true)
            .GroupBy(product => product.Categoryid)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Count);

        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        foreach (var category in categories)
        {
            category.Products = Enumerable.Range(0, sellableCounts.GetValueOrDefault(category.Id))
                .Select(_ => new Product { Stockquantity = 1 })
                .ToList();
        }

        return View(categories);
    }
}
