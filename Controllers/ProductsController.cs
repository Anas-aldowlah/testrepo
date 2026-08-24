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

}

