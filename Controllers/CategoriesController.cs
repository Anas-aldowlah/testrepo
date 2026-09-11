using Microsoft.AspNetCore.Mvc;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Services.Caching;

namespace YAGOT_2._0.Controllers;

[ServiceFilter(typeof(SiteStatusFilter))]
public class CategoriesController : Controller
{
    private readonly IStorefrontCacheService _storefrontCacheService;

    public CategoriesController(IStorefrontCacheService storefrontCacheService)
    {
        _storefrontCacheService = storefrontCacheService;
    }

    public async Task<IActionResult> Index()
    {
        var categorySummaries = await _storefrontCacheService.GetCategoriesListAsync(HttpContext.RequestAborted);
        return View(categorySummaries);
    }
}
