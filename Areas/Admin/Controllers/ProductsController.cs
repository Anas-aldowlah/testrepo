using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

namespace Yagot.Areas.Admin.Controllers;

[Area("Admin")]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public class ProductsController : Controller
{
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

    public async Task<IActionResult> Index()
    {
        // عرض كل المنتجات
        var productsFromDb = await _context.Products.Include(p => p.Category).ToListAsync();
        var categoriesFromDb = await _context.Categories.ToListAsync();
        var model = new ViewModels
        {
            Products = productsFromDb,
            Categories = categoriesFromDb
        };

        return View(model);
    }

    public async Task<IActionResult> trash()
    {
        var productsFromDb = await _context.Products.Include(p => p.Category).ToListAsync();
        var categoriesFromDb = await _context.Categories.ToListAsync();
        var model = new ViewModels
        {
            Products = productsFromDb,
            Categories = categoriesFromDb
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
                Id = await _productService.NextCounter(), // تجيب ID جديد
                Name = productvw.Name,
                Description = productvw.Description,
                Price = productvw.Price,
                Stockquantity = productvw.Stockquantity,
                Imageurl = filename != null ? "/images/products/" + filename : "/images/products/6389130_camera_interface_movie_picture_zoom_icon.png",
                Categoryid = productvw.Categoryid,
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
                fileName = await _imageService.UpdateImage(productVW.Imagefile, "products", fileName);
                product.Imageurl = fileName != null ? "/images/products/" + fileName : "/images/products/6389130_camera_interface_movie_picture_zoom_icon.png";
            }

            // 4. تحديث بيانات المنتج
            product.Name = productVW.Name;
            product.Description = productVW.Description;
            product.Price = productVW.Price;
            product.Stockquantity = productVW.Stockquantity;
            product.Categoryid = productVW.Categoryid;
            product.Imageurl = fileName;

            _context.Update(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // إذا فشل الموديل أو حدث خطأ، أعد تحميل التصنيفات
        ViewBag.Categoryid = _context.Categories.ToList();
        return View(productVW);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null)
        {
            return NotFound();
        }
        if (!string.IsNullOrEmpty(product.Imageurl))
        {
            if (product.Imageurl != "images/products/6389130_camera_interface_movie_picture_zoom_icon.png")
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