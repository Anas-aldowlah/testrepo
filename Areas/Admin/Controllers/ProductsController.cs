using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services;

namespace Yagot.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Developer")]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public class ProductsController : Controller
{
    private const string DeletedProductImagePath = "/images/products/6389130_camera_interface_movie_picture_zoom_icon.png";
    private const string DeletedProductImagePathLegacy = "images/products/6389130_camera_interface_movie_picture_zoom_icon.png";

    private readonly ProductService _productService;
    private readonly NeondbContext _context;
    private readonly Image _imageService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        ProductService productService,
        NeondbContext context,
        IWebHostEnvironment webHostEnvironment,
        Image imageService,
        ILogger<ProductsController> logger)
    {
        _productService = productService;
        _context = context;
        _imageService = imageService;
        _logger = logger;
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
            Categories = await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            TotalActiveProducts = await activeQuery.CountAsync(),
            TotalArchivedProducts = await _context.Products.CountAsync(p =>
                p.Stockquantity == 0 &&
                (p.Imageurl == DeletedProductImagePath || p.Imageurl == DeletedProductImagePathLegacy)),
            LowStockCount = await activeQuery.CountAsync(p =>
                p.Stockquantity > 0 &&
                ((p.StockUnit == "Ml" && p.VolumeMl != null && p.Stockquantity < p.VolumeMl * 5) ||
                 (p.StockUnit != "Ml" && p.Stockquantity < 5))),
            OutOfStockCount = await activeQuery.CountAsync(p => p.Stockquantity <= 0),
            Search = search ?? string.Empty
        };

        return View(model);
    }

    [Authorize(Roles = "Admin,Developer")]
    public async Task<IActionResult> trash(int page = 1, int pageSize = 10)
    {
        var archivedQuery = _context.Products
            .AsNoTracking()
            .Where(p => p.Stockquantity == 0 &&
                (p.Imageurl == DeletedProductImagePath || p.Imageurl == DeletedProductImagePathLegacy));

        var model = new AdminProductTrashViewModel
        {
            Products = await PagedResult<Product>.CreateAsync(
                archivedQuery.Include(p => p.Category).OrderByDescending(p => p.Createdat).ThenBy(p => p.Name),
                page,
                pageSize),
            Categories = await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            TotalArchivedProducts = await archivedQuery.CountAsync()
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.Categoryid = new SelectList(_context.Categories.ToList(), "Id", "Name");
        ViewBag.Brands = _context.Products.Where(p => !string.IsNullOrWhiteSpace(p.Brand)).Select(p => p.Brand!.Trim()).Distinct().ToList();
        return View(new ProductVW
        {
            StockUnit = "Piece",
            RetailPrices = []
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Create(ProductVW productvw)
    {
        NormalizeRetailRows(productvw);
        try
        {
            ValidateProductConfiguration(productvw, isCreate: true, existingProduct: null);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var stockQuantity = CalculateInitialStock(productvw);
            if (stockQuantity > 1000000)
            {
                ModelState.AddModelError(nameof(ProductVW.Stockquantity), "The calculated stock quantity must not exceed 1,000,000.");
                return ValidationProblem(ModelState);
            }

            string? filename = await _imageService.UploadImage(productvw.Imagefile, "products");
            var newProduct = new Product
            {
                Name = productvw.Name,
                Description = productvw.Description,
                Price = productvw.Price,
                Stockquantity = stockQuantity,
                StockUnit = NormalizeStockUnit(productvw.StockUnit),
                VolumeMl = NormalizeStockUnit(productvw.StockUnit) == "Ml" ? productvw.VolumeMl : null,
                IsRetailEnabled = NormalizeStockUnit(productvw.StockUnit) == "Ml" && productvw.IsRetailEnabled,
                Imageurl = filename != null ? "/images/products/" + filename : DeletedProductImagePath,
                Categoryid = productvw.Categoryid,
                Brand = productvw.Brand,
                Createdat = DateTime.Now
            };

            foreach (var price in ActiveRetailRows(productvw))
            {
                newProduct.RetailPrices.Add(new ProductRetailPrice
                {
                    SizeMl = price.SizeMl,
                    Price = price.Price,
                    IsActive = price.IsActive
                });
            }

            _context.Products.Add(newProduct);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        catch (OverflowException exception)
        {
            _logger.LogWarning(exception, "Rejected product creation because its initial stock calculation overflowed.");
            ModelState.AddModelError(nameof(ProductVW.Stockquantity), "The calculated stock quantity exceeds the supported numeric range.");
            return ValidationProblem(ModelState);
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        var product = await _context.Products
            .Include(p => p.RetailPrices)
            .SingleOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();

        var model = ToProductViewModel(product);
        ViewBag.Categories = await _productService.GetCategoriesAsync();
        ViewBag.Brands = await _context.Products.Where(p => !string.IsNullOrWhiteSpace(p.Brand)).Select(p => p.Brand!.Trim()).Distinct().ToListAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductVW productVW)
    {
        var product = await _context.Products
            .Include(p => p.RetailPrices)
            .SingleOrDefaultAsync(p => p.Id == productVW.Id);
        if (product == null) return NotFound();

        NormalizeRetailRows(productVW);
        try
        {
            ValidateProductConfiguration(productVW, isCreate: false, existingProduct: product);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            string? fileName = productVW.Existingimage;
            if (productVW.Imagefile != null && productVW.Imagefile.Length > 0)
            {
                fileName = await _imageService.UpdateImage(productVW.Imagefile, "products", fileName ?? string.Empty);
                product.Imageurl = fileName != null ? "/images/products/" + fileName : DeletedProductImagePath;
            }

            var stockUnit = NormalizeStockUnit(productVW.StockUnit);
            product.Name = productVW.Name;
            product.Description = productVW.Description;
            product.Price = productVW.Price;
            product.Categoryid = productVW.Categoryid;
            product.Brand = productVW.Brand;
            product.StockUnit = stockUnit;
            product.VolumeMl = stockUnit == "Ml" ? productVW.VolumeMl : null;
            product.IsRetailEnabled = stockUnit == "Ml" && productVW.IsRetailEnabled;
            product.Imageurl = fileName != null && !fileName.StartsWith("/images/", StringComparison.OrdinalIgnoreCase)
                ? "/images/products/" + fileName
                : fileName;

            SyncRetailPrices(product, productVW);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        catch (OverflowException exception)
        {
            _logger.LogWarning(exception, "Rejected update for product {ProductId} because a numeric value overflowed.", productVW.Id);
            ModelState.AddModelError(string.Empty, "A product numeric value exceeds the supported range.");
            return ValidationProblem(ModelState);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStock(StockAdjustmentViewModel input)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var product = await _context.Products.SingleOrDefaultAsync(p => p.Id == input.Id);
        if (product == null) return NotFound();

        int amount;
        try
        {
            if (product.StockUnit == "Ml" && input.VolumeMl is not > 0)
            {
                ModelState.AddModelError(nameof(StockAdjustmentViewModel.VolumeMl), "A valid bottle volume is required for ml products.");
                return ValidationProblem(ModelState);
            }

            amount = product.StockUnit == "Ml"
                ? checked(input.AddedBottleCount * input.VolumeMl!.Value)
                : input.AddedStockQuantity;
        }
        catch (OverflowException exception)
        {
            _logger.LogWarning(exception, "Rejected stock adjustment for product {ProductId} because the amount overflowed.", input.Id);
            ModelState.AddModelError(string.Empty, "The stock adjustment exceeds the supported numeric range.");
            return ValidationProblem(ModelState);
        }

        if (amount <= 0)
        {
            ModelState.AddModelError(string.Empty, "Stock adjustment quantity must be greater than zero.");
            return ValidationProblem(ModelState);
        }

        try
        {
            var resultingStock = checked(product.Stockquantity + amount);
            if (resultingStock > 1000000)
            {
                ModelState.AddModelError(string.Empty, "The resulting stock quantity must not exceed 1,000,000.");
                return ValidationProblem(ModelState);
            }

            product.Stockquantity = resultingStock;
        }
        catch (OverflowException exception)
        {
            _logger.LogWarning(exception, "Rejected stock adjustment for product {ProductId} because the resulting stock overflowed.", input.Id);
            ModelState.AddModelError(string.Empty, "The resulting stock quantity exceeds the supported numeric range.");
            return ValidationProblem(ModelState);
        }
        await _context.SaveChangesAsync();
        TempData["Message"] = "تمت إضافة المخزون بنجاح.";
        return RedirectToAction(nameof(Edit), new { id = input.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null) return NotFound();

        if (!string.IsNullOrEmpty(product.Imageurl) &&
            !string.Equals(product.Imageurl, DeletedProductImagePath, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(product.Imageurl, DeletedProductImagePathLegacy, StringComparison.OrdinalIgnoreCase))
        {
            var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", Path.GetFileName(product.Imageurl));
            if (System.IO.File.Exists(imagePath))
                System.IO.File.Delete(imagePath);
        }

        product.Imageurl = DeletedProductImagePath;
        product.Stockquantity = 0;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private static ProductVW ToProductViewModel(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Description = product.Description,
        Price = product.Price,
        Stockquantity = product.Stockquantity,
        StockUnit = product.StockUnit,
        VolumeMl = product.VolumeMl,
        IsRetailEnabled = product.IsRetailEnabled,
        Existingimage = product.Imageurl,
        Categoryid = product.Categoryid,
        Brand = product.Brand,
        RetailPrices = product.RetailPrices
            .OrderBy(price => price.SizeMl)
            .Select(price => new ProductRetailPriceInput
            {
                Id = price.Id,
                SizeMl = price.SizeMl,
                Price = price.Price,
                IsActive = price.IsActive
            })
            .ToList()
    };

    private static void ValidateProductConfiguration(ProductVW model, bool isCreate, Product? existingProduct)
    {
        model.StockUnit = NormalizeStockUnit(model.StockUnit);

        if (model.StockUnit != "Ml" && model.IsRetailEnabled)
            throw new InvalidOperationException("لا يمكن تفعيل التجزئة إلا للمنتجات التي وحدتها مل.");

        if (model.StockUnit == "Ml")
        {
            if (model.VolumeMl is not > 0)
                throw new InvalidOperationException("VolumeMl is required for ml products.");

            if (!isCreate &&
                existingProduct != null &&
                existingProduct.Stockquantity > 0 &&
                !string.Equals(existingProduct.StockUnit, model.StockUnit, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("لا يمكن تغيير وحدة المخزون لمنتج لديه رصيد قائم. أنشئ منتجاً جديداً أو استخدم تسوية مخزون واضحة.");
            }

            if (!isCreate &&
                existingProduct?.StockUnit == "Ml" &&
                existingProduct.Stockquantity > 0 &&
                existingProduct.VolumeMl != model.VolumeMl)
            {
                throw new InvalidOperationException("لا يمكن تغيير حجم العبوة لمنتج لديه مخزون قائم. أنشئ منتجاً جديداً أو استخدم تسوية مخزون واضحة.");
            }
        }
        else
        {
            if (!isCreate &&
                existingProduct != null &&
                existingProduct.Stockquantity > 0 &&
                !string.Equals(existingProduct.StockUnit, model.StockUnit, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("لا يمكن تغيير وحدة المخزون لمنتج لديه رصيد قائم. أنشئ منتجاً جديداً أو استخدم تسوية مخزون واضحة.");
            }

            model.VolumeMl = null;
            model.IsRetailEnabled = false;
        }
    }

    private static int CalculateInitialStock(ProductVW model)
    {
        if (NormalizeStockUnit(model.StockUnit) == "Ml")
            return checked(model.Stockquantity * (model.VolumeMl ?? 0));

        return model.Stockquantity;
    }

    private static void NormalizeRetailRows(ProductVW model)
    {
        model.RetailPrices = model.RetailPrices
            .Where(price => price.SizeMl > 0 || price.Price > 0 || price.Id.HasValue)
            .ToList();
    }

    private static IEnumerable<ProductRetailPriceInput> ActiveRetailRows(ProductVW model)
    {
        if (NormalizeStockUnit(model.StockUnit) != "Ml" || !model.IsRetailEnabled)
            return [];

        return model.RetailPrices
            .Where(price => price.SizeMl > 0 && price.Price > 0)
            .GroupBy(price => price.SizeMl)
            .Select(group => group.Last());
    }

    private static void SyncRetailPrices(Product product, ProductVW model)
    {
        var activeInputs = ActiveRetailRows(model).ToList();
        var seenIds = activeInputs.Where(input => input.Id.HasValue).Select(input => input.Id!.Value).ToHashSet();

        foreach (var existing in product.RetailPrices)
            existing.IsActive = seenIds.Contains(existing.Id) && activeInputs.Any(input => input.Id == existing.Id && input.IsActive);

        foreach (var input in activeInputs)
        {
            var existing = input.Id.HasValue
                ? product.RetailPrices.FirstOrDefault(price => price.Id == input.Id.Value)
                : product.RetailPrices.FirstOrDefault(price => price.SizeMl == input.SizeMl);

            if (existing == null)
            {
                product.RetailPrices.Add(new ProductRetailPrice
                {
                    SizeMl = input.SizeMl,
                    Price = input.Price,
                    IsActive = input.IsActive
                });
            }
            else
            {
                existing.SizeMl = input.SizeMl;
                existing.Price = input.Price;
                existing.IsActive = input.IsActive;
            }
        }
    }

    private static string NormalizeStockUnit(string? stockUnit) =>
        string.Equals(stockUnit, "Ml", StringComparison.OrdinalIgnoreCase) ? "Ml" : "Piece";

}
