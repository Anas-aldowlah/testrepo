using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

namespace Yagot.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Developer")]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public class ProductsController : Controller
{
    private const string DeletedProductImagePath = "/images/products/6389130_camera_interface_movie_picture_zoom_icon.png";
    private const string DeletedProductImagePathLegacy = "images/products/6389130_camera_interface_movie_picture_zoom_icon.png";

    // DI
    private readonly ProductService _productService;

    private readonly NeondbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly Image _imageService;

    public ProductsController(ProductService productService, NeondbContext context, IWebHostEnvironment webHostEnvironment, Image imageService)
    {
        _productService = productService;

        _context = context;
        _webHostEnvironment = webHostEnvironment;
        _imageService = imageService;
    }

    public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10)
    {
        var activeQuery = _context.Products
            .AsNoTracking()
            .Where(p => !(p.Stockquantity == 0 &&
                (p.Imageurl == DeletedProductImagePath || p.Imageurl == DeletedProductImagePathLegacy)));

        var filteredQuery = activeQuery;
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            filteredQuery = filteredQuery.Where(p =>
                p.Name.Contains(search) ||
                (!string.IsNullOrWhiteSpace(p.Description) && p.Description.Contains(search)) ||
                (!string.IsNullOrWhiteSpace(p.Brand) && p.Brand.Contains(search)) ||
                (p.Category != null && p.Category.Name.Contains(search)));
        }

        var model = new AdminProductsIndexViewModel
        {
            Products = await PagedResult<Product>.CreateAsync(
                filteredQuery
                    .Include(p => p.Category)
                    .OrderByDescending(p => p.Createdat)
                    .ThenBy(p => p.Name),
                page,
                pageSize),
            Categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(),
            TotalActiveProducts = await activeQuery.CountAsync(),
            TotalArchivedProducts = await _context.Products.CountAsync(p =>
                p.Stockquantity == 0 &&
                (p.Imageurl == DeletedProductImagePath || p.Imageurl == DeletedProductImagePathLegacy)),
            LowStockCount = await activeQuery.CountAsync(p => p.Stockquantity > 0 && p.Stockquantity < 5),
            OutOfStockCount = await activeQuery.CountAsync(p => p.Stockquantity <= 0),
            Search = search ?? string.Empty
        };

        return View(model);
    }

    public async Task<IActionResult> trash(int page = 1, int pageSize = 10)
    {
        var archivedQuery = _context.Products
            .AsNoTracking()
            .Where(p => p.Stockquantity == 0 &&
                (p.Imageurl == DeletedProductImagePath || p.Imageurl == DeletedProductImagePathLegacy));

        var model = new AdminProductTrashViewModel
        {
            Products = await PagedResult<Product>.CreateAsync(
                archivedQuery
                    .Include(p => p.Category)
                    .OrderByDescending(p => p.Createdat)
                    .ThenBy(p => p.Name),
                page,
                pageSize),
            Categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(),
            TotalArchivedProducts = await archivedQuery.CountAsync()
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        //  تعرض الصفحة لاضافة المنتج
        var categories = _context.Categories.ToList();
        ViewBag.Categoryid = new SelectList(categories, "Id", "Name");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Create(ProductVW productvw)
    {
        //  تحفظ المنتج الذي تم اضافته

        if (ModelState.IsValid)
        {
            // رفع الصورة
            string? filename = await _imageService.UploadImage(productvw.Imagefile, "products");

            // انشاء منتج جديد
            var newProduct = new Product
            {
                Name = productvw.Name,
                Description = productvw.Description,
                Price = productvw.Price,
                Stockquantity = productvw.Stockquantity,
                Imageurl = filename != null ? "/images/products/" + filename : "/images/products/6389130_camera_interface_movie_picture_zoom_icon.png",
                Categoryid = productvw.Categoryid,
                Brand = productvw.Brand,
                Createdat = DateTime.Now
            };
            // الحفظ في قاعدة البيانات
            _context.Products.Add(newProduct);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        // بعد الحفظ ترسل البيانات للـ view وترجع المستخدم لصفحة المنتجات
        ViewBag.Categoryid = new SelectList(_context.Categories, "Id", "Name", productvw.Categoryid);
        return View(productvw);
    }

    public async Task<IActionResult> Edit(int id)
    {
        // عرض الصفحة
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null) return NotFound();

        ProductVW model = new ProductVW()
        {
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Stockquantity = product.Stockquantity,
            Existingimage = product.Imageurl,
            Categoryid = product.Categoryid,
            Brand = product.Brand,
        };

        ViewBag.Categories = await _productService.GetCategoriesAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductVW productVW)
    {
        // حفظ المنتجات بعد التعديل
        if (ModelState.IsValid)
        {
            // 1. جلب المنتج من قاعدة البيانات بشكل غير متزامن
            var product = await _context.Products.FindAsync(productVW.Id);
            if (product == null) return NotFound();

            // احتفظ باسم الملف الحالي (القديم)
            string? fileName = productVW.Existingimage;

            // 2. التحقق مما إذا كان المستخدم قد رفع صورة جديدة
            if (productVW.Imagefile != null && productVW.Imagefile.Length > 0)
            {
                fileName = await _imageService.UpdateImage(productVW.Imagefile, "products", fileName ?? string.Empty);
                product.Imageurl = fileName != null ? "/images/products/" + fileName : "/images/products/6389130_camera_interface_movie_picture_zoom_icon.png";
            }

            // 4. تحديث بيانات المنتج
            product.Name = productVW.Name;
            product.Description = productVW.Description;
            product.Price = productVW.Price;
            product.Stockquantity = productVW.Stockquantity;
            product.Categoryid = productVW.Categoryid;
            product.Brand = productVW.Brand;
            product.Imageurl = fileName != null && !fileName.StartsWith("/images/", StringComparison.OrdinalIgnoreCase)
                ? "/images/products/" + fileName
                : fileName;

            _context.Update(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // إذا فشل الموديل أو حدث خطأ، أعد تحميل التصنيفات
        ViewBag.Categoryid = _context.Categories.ToList();
        return View(productVW);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null)
        {
            return NotFound();
        }
        if (!string.IsNullOrEmpty(product.Imageurl))
        {
            if (!string.Equals(product.Imageurl, DeletedProductImagePath, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(product.Imageurl, DeletedProductImagePathLegacy, StringComparison.OrdinalIgnoreCase))
            {
                var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
         "images", "products", Path.GetFileName(product.Imageurl));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }
            }
        }
        product.Imageurl = "/images/products/6389130_camera_interface_movie_picture_zoom_icon.png";
        product.Stockquantity = 0;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
