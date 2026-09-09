using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;
using YAGOT_2._0.Services.Caching;
using static YAGOT_2._0.Services.DealingAPI;

namespace Yagot.Controllers;

[ServiceFilter(typeof(SiteStatusFilter))]
public class HomeController : Controller
{
    private readonly NeondbContext _context;
    private readonly IVisitService _visitService;
    private readonly StoreSettingsService _storeSettingsService;
    private readonly ProductCatalogService _catalogService;
    private readonly IStorefrontCacheService _storefrontCacheService;

    public HomeController(
        NeondbContext context,
        IVisitService visitService,
        StoreSettingsService storeSettingsService,
        ProductCatalogService catalogService,
        IStorefrontCacheService storefrontCacheService)
    {
        _context = context;
        _visitService = visitService;
        _storeSettingsService = storeSettingsService;
        _catalogService = catalogService;
        _storefrontCacheService = storefrontCacheService;
    }

    public async Task<IActionResult> Index(int? categoryId)
    {
        var showcase = await _storefrontCacheService.GetHomeShowcaseAsync(HttpContext.RequestAborted);
        var categoriesFromDb = (await _catalogService.GetCategoriesAsync(HttpContext.RequestAborted)).ToList();
        var brands = (await _catalogService.GetBrandsAsync(HttpContext.RequestAborted)).ToList();

        var model = new ViewModels
        {
            NewArrivals = showcase.GetNewArrivalProducts(),
            HeroSlides = showcase.GetHeroSlideProducts(),
            BestSellingProducts = showcase.GetBestSellingProducts(),
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
