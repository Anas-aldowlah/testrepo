using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

namespace Yagot.Controllers;

public class HomeController : Controller
{
    private readonly NeondbContext _context;

    public HomeController(NeondbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? categoryId)
    {
        var productsFromDb = await _context.Products.Include(p => p.Category).ToListAsync();
        var categoriesFromDb = await _context.Categories.ToListAsync();
        var model = new ViewModels
        {
            Products = productsFromDb,
            Categories = categoriesFromDb
        };
        ViewBag.SelectedCategoryId = categoryId;

        if (categoryId.HasValue)
        {
            var selectedCategory = model.Categories.FirstOrDefault(c => c.Id == categoryId.Value);
            ViewBag.SelectedCategoryName = selectedCategory?.Name;

          
        }
        else
        {
            var allProducts = model.Products;
        }

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
