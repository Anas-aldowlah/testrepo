using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

namespace YAGOT_2._0.Controllers;

[ServiceFilter(typeof(SiteStatusFilter))]
public class ProductsController : Controller
{
    private readonly NeondbContext _context;
    private readonly ProductCatalogService _catalogService;

    public ProductsController(NeondbContext context, ProductCatalogService catalogService)
    {
        _context = context;
        _catalogService = catalogService;
    }

    public async Task<IActionResult> Index(ProductsCatalogRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var model = await _catalogService.GetCatalogAsync(request, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        // تعرض تفاصيل المنتج محدد
        var product = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.RetailPrices.Where(price =>
                price.IsActive && price.SizeMl > 0 && price.Price > 0))
            .FirstOrDefaultAsync(p => p.Id == id);
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
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.RetailPrices.Where(price =>
                price.IsActive && price.SizeMl > 0 && price.Price > 0))
            .Where(p => validIds.Contains(p.Id))
            .ToListAsync();

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

