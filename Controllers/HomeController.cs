using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;
using YAGOT_2._0.Services.Caching;
using YAGOT_2._0.Core.Capabilities;

namespace Yagot.Controllers;

[ServiceFilter(typeof(SiteStatusFilter))]
public class HomeController : Controller
{
    private readonly NeondbContext _context;
    private readonly IVisitService _visitService;
    private readonly StoreSettingsService _storeSettingsService;
    private readonly ProductCatalogService _catalogService;
    private readonly IStorefrontCacheService _storefrontCacheService;
    private readonly ICapabilityEvaluator _capabilityEvaluator;

    public HomeController(
        NeondbContext context,
        IVisitService visitService,
        StoreSettingsService storeSettingsService,
        ProductCatalogService catalogService,
        IStorefrontCacheService storefrontCacheService,
        ICapabilityEvaluator capabilityEvaluator)
    {
        _context = context;
        _visitService = visitService;
        _storeSettingsService = storeSettingsService;
        _catalogService = catalogService;
        _storefrontCacheService = storefrontCacheService;
        _capabilityEvaluator = capabilityEvaluator;
    }

    public async Task<IActionResult> Index(int? categoryId)
    {
        var canViewProducts = _capabilityEvaluator.IsFeatureEnabled(CapabilityFeatureCodes.ProductView);
        var showcase = canViewProducts
            ? await _storefrontCacheService.GetHomeShowcaseAsync(HttpContext.RequestAborted)
            : null;
        var categoriesFromDb = (await _catalogService.GetCategoriesAsync(HttpContext.RequestAborted)).ToList();
        var brands = canViewProducts && _capabilityEvaluator.IsFeatureEnabled(CapabilityFeatureCodes.ProductBrands)
            ? (await _catalogService.GetBrandsAsync(HttpContext.RequestAborted)).ToList()
            : [];

        var model = new ViewModels
        {
            NewArrivals = showcase?.GetNewArrivalProducts() ?? [],
            HeroSlides = showcase?.GetHeroSlideProducts() ?? [],
            BestSellingProducts = showcase?.GetBestSellingProducts() ?? [],
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
