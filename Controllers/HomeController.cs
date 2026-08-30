using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;
using static YAGOT_2._0.Services.DealingAPI;

namespace Yagot.Controllers;

[ServiceFilter(typeof(SiteStatusFilter))]
public class HomeController : Controller
{
    private readonly NeondbContext _context;
    private readonly IVisitService _visitService;
    private readonly StoreSettingsService _storeSettingsService;

    public HomeController(
        NeondbContext context,
        IVisitService visitService,
        StoreSettingsService storeSettingsService)
    {
        _context = context;
        _visitService = visitService;
        _storeSettingsService = storeSettingsService;
    }

    public async Task<IActionResult> Index(int? categoryId)
    {
        var allProducts = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.RetailPrices.Where(price =>
                price.IsActive && price.SizeMl > 0 && price.Price > 0))
            .OrderByDescending(p => p.Createdat)
            .ToListAsync();

        var productsFromDb = allProducts;
        var categoriesFromDb = await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        var model = new ViewModels
        {
            Products = productsFromDb,
            Categories = categoriesFromDb,
            StoreSettings = await _storeSettingsService.GetSettingsAsync()
        };
        ViewBag.SelectedCategoryId = categoryId;

        if (categoryId.HasValue)
        {
            var selectedCategory = model.Categories.FirstOrDefault(c => c.Id == categoryId.Value);
            ViewBag.SelectedCategoryName = selectedCategory?.Name;
        }

        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [AllowAnonymous]
    [HttpGet("/loaderio-03aae8d3-af62-4d5c-9e36-16d1795c99cf.txt")]
    public IActionResult LoaderIoVerification()
    {
        return Content(
            "loaderio-03aae8d3-af62-4d5c-9e36-16d1795c99cf",
            "text/plain"
        );
    }
}
