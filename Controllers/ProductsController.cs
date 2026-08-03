using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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

    public ProductsController(NeondbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? categoryId, string? search, string? brand)
    {
        search = search?.Trim();
        brand = brand?.Trim();
        if (brand == "-")
        {
            brand = null;
        }

        var productsQuery = _context.Products
            .Include(p => p.Category)
            .Where(p => p.Stockquantity > 0);

        if (categoryId.HasValue && categoryId.Value != -100)
        {
            productsQuery = productsQuery.Where(p => p.Categoryid == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(brand))
        {
            var normalizedBrand = brand.ToLower();
            productsQuery = productsQuery.Where(p => p.Brand != null && p.Brand.ToLower() == normalizedBrand);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            productsQuery = productsQuery.Where(p =>
                p.Name.Contains(search) ||
                (!string.IsNullOrWhiteSpace(p.Description) && p.Description.Contains(search)) ||
                (!string.IsNullOrWhiteSpace(p.Brand) && p.Brand.Contains(search)));
        }

        var productsFromDb = await productsQuery.Take(100).ToListAsync();
        var categoriesFromDb = await _context.Categories.ToListAsync();
        var model = new ViewModels
        {
            Products = productsFromDb,
            Categories = categoriesFromDb
        };
        var categoryList = model.Categories.ToList();
        ViewBag.Categories = categoryList.ToList();

        ViewBag.Search = search ?? string.Empty;
        ViewBag.Brand = brand ?? string.Empty;

        if (categoryId.HasValue)
        {
            var category = categoryList.FirstOrDefault(c => c.Id == categoryId.Value);
            // تجيب اسم ورقم القسم للصفحة اذا المستخدم حدد ذلك
            ViewBag.CategoryName = category?.Name;
            ViewBag.CategoryId = categoryId;
        }
        else
        {
            ViewBag.CategoryId = null;
        }

        return View(model.Products.ToList());
    }

    public async Task<IActionResult> Details(int id)
    {
        // تعرض تفاصيل المنتج محدد
        var product = await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();
        return View(product);
    }

    public async Task<IActionResult> trash(int id)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();
        return View(product);
    }
}

