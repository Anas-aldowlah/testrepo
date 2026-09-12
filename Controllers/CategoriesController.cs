using Microsoft.AspNetCore.Mvc;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Services.Caching;

namespace YAGOT_2._0.Controllers;

[ServiceFilter(typeof(SiteStatusFilter))]
public class CategoriesController : Controller
{
    private readonly IStorefrontCacheService _storefrontCacheService;
    private readonly ICapabilityEvaluator _capabilityEvaluator;

    public CategoriesController(
        IStorefrontCacheService storefrontCacheService,
        ICapabilityEvaluator capabilityEvaluator)
    {
        _storefrontCacheService = storefrontCacheService;
        _capabilityEvaluator = capabilityEvaluator;
    }

    public async Task<IActionResult> Index()
    {
        if (!_capabilityEvaluator.IsFeatureEnabled(CapabilityFeatureCodes.CategoryView))
        {
            return NotFound();
        }

        var categorySummaries = await _storefrontCacheService.GetCategoriesListAsync(HttpContext.RequestAborted);
        return View(categorySummaries);
    }
}
