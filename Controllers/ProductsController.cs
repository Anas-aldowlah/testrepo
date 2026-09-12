using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

namespace YAGOT_2._0.Controllers;

[ServiceFilter(typeof(SiteStatusFilter))]
public class ProductsController : Controller
{
    private readonly NeondbContext _context;
    private readonly ProductCatalogService _catalogService;
    private readonly ICapabilityEvaluator _capabilityEvaluator;

    public ProductsController(
        NeondbContext context,
        ProductCatalogService catalogService,
        ICapabilityEvaluator capabilityEvaluator)
    {
        _context = context;
        _catalogService = catalogService;
        _capabilityEvaluator = capabilityEvaluator;
    }

    public async Task<IActionResult> Index(ProductsCatalogRequest request, CancellationToken cancellationToken)
    {
        if (request.CategoryId.HasValue && request.CategoryId.Value != -100 &&
            !_capabilityEvaluator.IsFeatureEnabled(CapabilityFeatureCodes.CategoryProductsView))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return BadRequest(ModelState);

            request.MinPrice = null;
            request.MaxPrice = null;
        }

        var model = await _catalogService.GetCatalogAsync(request, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        // تعرض تفاصيل المنتج محدد
        var product = (await _context.Products
            .Where(p => p.Id == id)
            .ToProductCardsAsync(HttpContext.RequestAborted))
            .SingleOrDefault();
        if (product == null) return NotFound();
        product.RetailPrices = ProductRetailAvailability.GetCustomerUsablePrices(product).ToList();
        return View(product);
    }

    [HttpGet]
    public async Task<IActionResult> RecentlyViewed([FromQuery] int[] ids)
    {
        if (ids == null || ids.Length == 0)
            return PartialView("_RecentlyViewedCard", new List<Product>());

        var validIds = ids.Where(id => id > 0).Distinct().Take(6).ToList();
        if (!validIds.Any())
            return PartialView("_RecentlyViewedCard", new List<Product>());

        var products = await _context.Products
            .Where(p => validIds.Contains(p.Id))
            .ToProductCardsAsync(HttpContext.RequestAborted);

        var orderedProducts = new List<Product>();
        foreach (var id in validIds)
        {
            var p = products.FirstOrDefault(x => x.Id == id);
            if (p != null)
            {
                p.RetailPrices = ProductRetailAvailability.GetCustomerUsablePrices(p).ToList();
                orderedProducts.Add(p);
            }
        }

        return PartialView("_RecentlyViewedCard", orderedProducts);
    }
}

