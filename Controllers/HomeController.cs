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
        var productsFromDb = await _context.Products.Include(p => p.Category).ToListAsync();
        var categoriesFromDb = await _context.Categories.ToListAsync();
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
        else
        {
            var allProducts = model.Products;
        }

        return View(model);
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Contact()
    {
        return View(new ContactMessageVM());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Contact(ContactMessageVM model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        ModelState.AddModelError(
            string.Empty,
            "خدمة استقبال الرسائل غير متاحة مؤقتاً. يرجى استخدام إحدى قنوات التواصل المباشرة والمحاولة لاحقاً.");
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
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
