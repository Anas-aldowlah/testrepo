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
    private readonly ProductCatalogService _catalogService;

    public HomeController(
        NeondbContext context,
        IVisitService visitService,
        StoreSettingsService storeSettingsService,
        ProductCatalogService catalogService)
    {
        _context = context;
        _visitService = visitService;
        _storeSettingsService = storeSettingsService;
        _catalogService = catalogService;
    }

    public async Task<IActionResult> Index(int? categoryId)
    {
        var newArrivals = await _context.Products
            .OrderByDescending(p => p.Createdat)
            .Take(8)
            .ToProductCardsAsync(HttpContext.RequestAborted);

        var heroSlides = await _context.Products
            .Where(product => product.Imageurl != null && product.Imageurl.Trim() != string.Empty)
            .OrderByDescending(product => product.Createdat)
            .Take(12)
            .ToHeroProductsAsync(HttpContext.RequestAborted);

        if (heroSlides.Count == 0)
            heroSlides = newArrivals.Take(6).ToList();

        const int bestSellersLimit = 8;
        const int minBestSellersCount = 3;

        var bestSellingProducts = await _context.Products
            .WhereSellable(true)
            .Where(p => p.TotalSold > 0)
            .OrderByDescending(p => p.TotalSold)
            .ThenByDescending(p => p.Createdat)
            .ThenByDescending(p => p.Id)
            .Take(bestSellersLimit)
            .ToProductCardsAsync(HttpContext.RequestAborted);

        if (bestSellingProducts.Count < minBestSellersCount)
        {
            bestSellingProducts.Clear();
        }

        var categoriesFromDb = (await _catalogService.GetCategoriesAsync(HttpContext.RequestAborted)).ToList();
        var brands = (await _catalogService.GetBrandsAsync(HttpContext.RequestAborted)).ToList();
        var model = new ViewModels
        {
            NewArrivals = newArrivals,
            HeroSlides = heroSlides,
            BestSellingProducts = bestSellingProducts,
            Brands = brands,
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
    [HttpGet("/loaderio-30f76365-7ae0-496b-a192-ccfe32fa6ef1.txt")]
    public IActionResult LoaderIoVerification()
    {
        return Content(
            "loaderio-30f76365-7ae0-496b-a192-ccfe32fa6ef1",
            "text/plain"
        );
    }
}
