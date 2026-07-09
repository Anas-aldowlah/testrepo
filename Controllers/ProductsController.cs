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

    public async Task<IActionResult> Index(int? categoryId)
    {
        var productsFromDb = await _context.Products.Include(p => p.Category).Where(p => p.Stockquantity > 0).ToListAsync();
        var categoriesFromDb = await _context.Categories.ToListAsync();
        var model = new ViewModels
        {
            Products = productsFromDb,
            Categories = categoriesFromDb
        };
        var categoryList = model.Categories.ToList();
        ViewBag.Categories = categoryList.ToList();

        if (categoryId.HasValue)
        {
            var category = categoryList.FirstOrDefault(c => c.Id == categoryId.Value);
            // تجيب اسم ورقم القسم للصفحة اذا المستخدم حدد ذلك
            ViewBag.CategoryName = category?.Name;
            ViewBag.CategoryId = categoryId;

            // فلترة: يعرض للمستخدم المنتجات لقسم محدد
            List<Product> allProducts = model.Products.Where(p => p.Categoryid == categoryId).ToList();

            // تعرض جميع المنتجات وتعتبر حالة خاصة وهذا الرقم (-100) مميز
            if (categoryId == -100)
            {
                allProducts = model.Products.ToList();
            }
            // ترجع المنتجات المفلترة للصفحة
            return View(allProducts.ToList());
        }

        // اذا المستخدم لم يختر اي قسم فبترسل له جميع المنتجات
        List<Product> allProducts_all = model.Products.ToList();

        ViewBag.CategoryId = null;
        return View(allProducts_all);
    }

    public async Task<IActionResult> Details(int id)
    {
        // تعرض تفاصيل المنتج محدد
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
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