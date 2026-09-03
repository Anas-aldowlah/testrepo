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
    private readonly IInventoryService _inventoryService;
    private readonly IBestSellerService _bestSellerService;
    private readonly ProductCatalogService _catalogService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        ProductService productService,
        NeondbContext context,
        IWebHostEnvironment webHostEnvironment,
        Image imageService,
        IInventoryService inventoryService,
        IBestSellerService bestSellerService,
        ProductCatalogService catalogService,
        ILogger<ProductsController> logger)
    {
        _productService = productService;
        _context = context;
        _imageService = imageService;
        _inventoryService = inventoryService;
        _bestSellerService = bestSellerService;
        _catalogService = catalogService;
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

        var inventorySummary = await _context.Products
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Active = group.Count(p => !(p.Stockquantity == 0 &&
                    (p.Imageurl == DeletedProductImagePath || p.Imageurl == DeletedProductImagePathLegacy))),
                Archived = group.Count(p => p.Stockquantity == 0 &&
                    (p.Imageurl == DeletedProductImagePath || p.Imageurl == DeletedProductImagePathLegacy)),
                LowStock = group.Count(p =>
                    !(p.Stockquantity == 0 &&
                      (p.Imageurl == DeletedProductImagePath || p.Imageurl == DeletedProductImagePathLegacy)) &&
                    p.Stockquantity > 0 &&
                    ((p.StockUnit == "Ml" && p.VolumeMl != null && p.Stockquantity < p.VolumeMl * 5) ||
                     (p.StockUnit != "Ml" && p.Stockquantity < 5))),
                OutOfStock = group.Count(p =>
                    !(p.Stockquantity == 0 &&
                      (p.Imageurl == DeletedProductImagePath || p.Imageurl == DeletedProductImagePathLegacy)) &&
                    p.Stockquantity <= 0)
            })
            .SingleOrDefaultAsync();

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
            TotalActiveProducts = inventorySummary?.Active ?? 0,
            TotalArchivedProducts = inventorySummary?.Archived ?? 0,
            LowStockCount = inventorySummary?.LowStock ?? 0,
            OutOfStockCount = inventorySummary?.OutOfStock ?? 0,
            Search = search ?? string.Empty,
            BestSellersLastUpdated = await _bestSellerService.GetLastRefreshTimeAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Developer")]
    public async Task<IActionResult> RefreshBestSellers(string? returnUrl = null)
    {
        _logger.LogInformation("Admin user {AdminUser} initiated a manual Best Sellers refresh.", User.Identity?.Name);

        var result = await _bestSellerService.RefreshBestSellersAsync(HttpContext.RequestAborted);

        if (result.Success)
        {
            TempData["Success"] = $"تم تحديث قائمة الأكثر مبيعاً بنجاح ({result.TotalUnitsSold} وحدة مباعة خلال آخر {BestSellerConstants.BestSellerPeriodDays} يوماً).";
        }
        else
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "تعذر تحديث بيانات الأكثر مبيعاً حالياً. يرجى المحاولة لاحقاً."
                : $"فشل تحديث الأكثر مبيعاً: {result.ErrorMessage}";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
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
    public async Task<IActionResult> Create()
    {
        await PopulateCreateOptionsAsync();
        return View(new AdminProductCreateViewModel
        {
            StockUnit = "Piece",
            RetailPrices = []
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Create(AdminProductCreateViewModel input)
    {
        ValidateCreateConfiguration(input);
        var productvw = ToProductViewModel(input);
        ValidateRetailPrices(productvw, product: null, modelPrefix: null);

        var imageError = Image.GetValidationError(input.Imagefile);
        if (imageError != null)
            ModelState.AddModelError(nameof(AdminProductCreateViewModel.Imagefile), imageError);

        if (ModelState.IsValid &&
            !await _context.Categories.AsNoTracking().AnyAsync(category => category.Id == input.Categoryid!.Value))
        {
            ModelState.AddModelError(nameof(AdminProductCreateViewModel.Categoryid), "التصنيف المحدد غير صالح.");
        }

        if (!ModelState.IsValid)
            return await CreateValidationViewAsync(input);

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
            return await CreateValidationViewAsync(input);

        try
        {
            var stockQuantity = CalculateInitialStock(productvw);
            if (stockQuantity > 1000000)
            {
                ModelState.AddModelError(nameof(AdminProductCreateViewModel.Stockquantity), "يجب ألا تتجاوز كمية المخزون المحسوبة 1,000,000.");
                return await CreateValidationViewAsync(input);
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
            _catalogService.InvalidateMetadataCache();
            TempData["Success"] = "تمت إضافة المنتج بنجاح.";
            return RedirectToAction(nameof(Index));
        }
        catch (OverflowException exception)
        {
            _logger.LogWarning(exception, "Rejected product creation because its initial stock calculation overflowed.");
            ModelState.AddModelError(nameof(AdminProductCreateViewModel.Stockquantity), "تتجاوز كمية المخزون المحسوبة النطاق الرقمي المدعوم.");
            return await CreateValidationViewAsync(input);
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        var product = await _context.Products
            .Include(p => p.RetailPrices)
            .SingleOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();

        var historicallyUsedIds = await GetHistoricallyUsedRetailPriceIdsAsync(id);

        await PopulateEditOptionsAsync();
        return View(BuildEditViewModel(product, historicallyUsedIds: historicallyUsedIds));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        [Bind(Prefix = nameof(AdminProductEditViewModel.Product))] ProductVW productVW)
    {
        var product = await _context.Products
            .Include(p => p.RetailPrices)
            .SingleOrDefaultAsync(p => p.Id == productVW.Id);
        if (product == null) return NotFound();

        try
        {
            ValidateProductConfiguration(productVW, isCreate: false, existingProduct: product);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        ValidateRetailPrices(productVW, product, nameof(AdminProductEditViewModel.Product));

        if (!ModelState.IsValid)
            return await EditValidationViewAsync(productVW, product);

        try
        {
            string? fileName = productVW.Existingimage;
            if (productVW.Imagefile != null && productVW.Imagefile.Length > 0)
            {
                fileName = await _imageService.UpdateImage(productVW.Imagefile, "products", fileName ?? string.Empty);
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

            if (fileName == null)
            {
                product.Imageurl = DeletedProductImagePath;
            }
            else if (fileName.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
            {
                product.Imageurl = fileName;
            }
            else
            {
                product.Imageurl = "/images/products/" + fileName;
            }

            await SyncRetailPricesAsync(product, productVW);
            await _context.SaveChangesAsync();
            _catalogService.InvalidateMetadataCache();
            TempData["Success"] = "تم حفظ تعديلات المنتج بنجاح.";
            return RedirectToAction(nameof(Edit), new { id = product.Id });
        }
        catch (OverflowException exception)
        {
            _logger.LogWarning(exception, "Rejected update for product {ProductId} because a numeric value overflowed.", productVW.Id);
            ModelState.AddModelError(string.Empty, "تتجاوز إحدى القيم الرقمية للمنتج النطاق المدعوم.");
            return await EditValidationViewAsync(productVW, product);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Rejected update for product {ProductId} because retail pricing conflicted with persisted constraints.", productVW.Id);
            ModelState.AddModelError(string.Empty, "تعذر حفظ أسعار التجزئة. تحقق من الأحجام والأسعار ثم أعد المحاولة.");
            return await EditValidationViewAsync(productVW, product);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustStock(
        [Bind(Prefix = nameof(AdminProductEditViewModel.StockAdjustment))] StockAdjustmentViewModel input)
    {
        if (!ModelState.IsValid)
            return await StockValidationViewAsync(input);

        var request = new StockAdjustmentRequest(
            input.Id,
            input.Operation!.Value,
            input.SizeOption,
            input.CustomSizeMl,
            input.Quantity!.Value);
        var result = await _inventoryService.AdjustStockAsync(request, HttpContext.RequestAborted);
        if (!result.ProductFound)
            return NotFound();

        if (!result.Succeeded)
        {
            var key = string.IsNullOrEmpty(result.Field)
                ? nameof(AdminProductEditViewModel.StockAdjustment)
                : $"{nameof(AdminProductEditViewModel.StockAdjustment)}.{result.Field}";
            ModelState.AddModelError(key, result.Error!);
            return await StockValidationViewAsync(input);
        }

        TempData["Success"] = input.Operation == StockAdjustmentOperation.Subtract
            ? "تم خصم الكمية من المخزون بنجاح."
            : "تمت إضافة الكمية إلى المخزون بنجاح.";
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
        _catalogService.InvalidateMetadataCache();
        TempData["Success"] = "تم حذف المنتج بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    private static ProductVW ToProductViewModel(Product product, List<int>? historicallyUsedIds = null) => new()
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
                IsActive = price.IsActive,
                IsHistoricallyUsed = historicallyUsedIds != null && historicallyUsedIds.Contains(price.Id)
            })
            .ToList()
    };

    private static AdminProductEditViewModel BuildEditViewModel(
        Product product,
        ProductVW? submittedProduct = null,
        StockAdjustmentViewModel? submittedAdjustment = null,
        List<int>? historicallyUsedIds = null)
    {
        var isMlProduct = string.Equals(product.StockUnit, "Ml", StringComparison.OrdinalIgnoreCase);
        var baseSizeMl = product.VolumeMl.GetValueOrDefault();
        var sizes = new List<StockAdjustmentSizeViewModel>();
        if (isMlProduct && baseSizeMl > 0)
        {
            sizes.Add(new(
                StockAdjustmentSizeOptions.Base,
                baseSizeMl,
                $"{baseSizeMl} مل — الحجم الأساسي"));

            if (product.IsRetailEnabled)
            {
                sizes.AddRange(product.RetailPrices
                    .Where(price => price.IsActive && price.SizeMl > 0 && price.SizeMl < baseSizeMl)
                    .OrderBy(price => price.SizeMl)
                    .Select(price => new StockAdjustmentSizeViewModel(
                        $"{StockAdjustmentSizeOptions.RetailPrefix}{price.Id}",
                        price.SizeMl,
                        $"{price.SizeMl} مل — تجزئة")));
            }
        }

        return new AdminProductEditViewModel
        {
            Product = submittedProduct ?? ToProductViewModel(product, historicallyUsedIds),
            StockAdjustment = submittedAdjustment ?? new StockAdjustmentViewModel
            {
                Id = product.Id,
                Operation = StockAdjustmentOperation.Add,
                SizeOption = StockAdjustmentSizeOptions.Base,
                Quantity = 1
            },
            StockSizes = sizes
        };
    }

    private async Task PopulateEditOptionsAsync()
    {
        ViewBag.Categories = await _productService.GetCategoriesAsync();
        ViewBag.Brands = await _context.Products
            .AsNoTracking()
            .Where(product => !string.IsNullOrWhiteSpace(product.Brand))
            .Select(product => product.Brand!.Trim())
            .Distinct()
            .ToListAsync();
    }

    private static ProductVW ToProductViewModel(AdminProductCreateViewModel input) => new()
    {
        Categoryid = input.Categoryid.GetValueOrDefault(),
        Name = input.Name ?? string.Empty,
        Description = input.Description,
        Price = input.Price.GetValueOrDefault(),
        Stockquantity = input.Stockquantity.GetValueOrDefault(),
        StockUnit = input.StockUnit ?? string.Empty,
        VolumeMl = input.VolumeMl,
        IsRetailEnabled = input.IsRetailEnabled,
        Brand = input.Brand,
        Imagefile = input.Imagefile,
        RetailPrices = input.RetailPrices.Select(price => new ProductRetailPriceInput
        {
            Id = price.Id,
            SizeMl = price.SizeMl.GetValueOrDefault(),
            Price = price.Price.GetValueOrDefault(),
            IsActive = price.IsActive
        }).ToList()
    };

    private async Task PopulateCreateOptionsAsync()
    {
        ViewBag.Categoryid = new SelectList(
            await _context.Categories.AsNoTracking().OrderBy(category => category.Name).ToListAsync(),
            "Id",
            "Name");
        ViewBag.Brands = await _context.Products
            .AsNoTracking()
            .Where(product => !string.IsNullOrWhiteSpace(product.Brand))
            .Select(product => product.Brand!.Trim())
            .Distinct()
            .OrderBy(brand => brand)
            .ToListAsync();
    }

    private async Task<IActionResult> CreateValidationViewAsync(AdminProductCreateViewModel submitted)
    {
        await PopulateCreateOptionsAsync();
        return View("Create", submitted);
    }

    private void ValidateCreateConfiguration(AdminProductCreateViewModel input)
    {
        if (!string.Equals(input.StockUnit, "Ml", StringComparison.OrdinalIgnoreCase))
        {
            if (input.IsRetailEnabled)
                AddModelErrorIfAbsent(nameof(AdminProductCreateViewModel.IsRetailEnabled), "لا يمكن تفعيل التجزئة إلا للمنتجات التي وحدتها مل.");

            return;
        }

        if (!input.VolumeMl.HasValue)
            AddModelErrorIfAbsent(nameof(AdminProductCreateViewModel.VolumeMl), "حجم العبوة مطلوب للمنتجات التي تُدار بالمل.");
    }

    private void AddModelErrorIfAbsent(string key, string message)
    {
        if (!ModelState.TryGetValue(key, out var entry) || entry.Errors.Count == 0)
            ModelState.AddModelError(key, message);
    }

    private async Task<IActionResult> EditValidationViewAsync(ProductVW submitted, Product product)
    {
        ModelState.Remove($"{nameof(AdminProductEditViewModel.Product)}.{nameof(ProductVW.Stockquantity)}");
        ModelState.Remove($"{nameof(AdminProductEditViewModel.Product)}.{nameof(ProductVW.Existingimage)}");
        submitted.Stockquantity = product.Stockquantity;
        submitted.Existingimage = product.Imageurl;
        var historicallyUsedIds = await GetHistoricallyUsedRetailPriceIdsAsync(product.Id);
        foreach (var row in submitted.RetailPrices)
            row.IsHistoricallyUsed = row.Id.HasValue && historicallyUsedIds.Contains(row.Id.Value);
        await PopulateEditOptionsAsync();
        return View("Edit", BuildEditViewModel(product, submittedProduct: submitted));
    }

    private async Task<IActionResult> StockValidationViewAsync(StockAdjustmentViewModel submitted)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Include(candidate => candidate.RetailPrices)
            .SingleOrDefaultAsync(candidate => candidate.Id == submitted.Id);
        if (product == null)
            return NotFound();

        var historicallyUsedIds = await GetHistoricallyUsedRetailPriceIdsAsync(product.Id);
        await PopulateEditOptionsAsync();
        return View("Edit", BuildEditViewModel(
            product,
            submittedAdjustment: submitted,
            historicallyUsedIds: historicallyUsedIds));
    }

    private void ValidateRetailPrices(ProductVW model, Product? product, string? modelPrefix)
    {
        model.RetailPrices ??= new List<ProductRetailPriceInput>();
        if (!string.Equals(model.StockUnit, "Ml", StringComparison.OrdinalIgnoreCase) || !model.IsRetailEnabled)
            return;

        var knownIds = product?.RetailPrices.Select(price => price.Id).ToHashSet() ?? [];
        var submittedIds = new HashSet<int>();
        var sizeIndexes = new Dictionary<int, int>();
        var fieldPrefix = string.IsNullOrEmpty(modelPrefix) ? string.Empty : $"{modelPrefix}.";
        for (var index = 0; index < model.RetailPrices.Count; index++)
        {
            var row = model.RetailPrices[index];
            var prefix = $"{fieldPrefix}{nameof(ProductVW.RetailPrices)}[{index}]";

            if (product != null && row.Id.HasValue && !knownIds.Contains(row.Id.Value))
                AddModelErrorIfAbsent(prefix, "سعر التجزئة المحدد لا ينتمي إلى هذا المنتج.");
            else if (product != null && row.Id.HasValue && !submittedIds.Add(row.Id.Value))
                AddModelErrorIfAbsent(prefix, "لا يمكن إرسال سعر التجزئة نفسه أكثر من مرة.");

            if (row.SizeMl <= 0)
                AddModelErrorIfAbsent($"{prefix}.{nameof(ProductRetailPriceInput.SizeMl)}", "حجم التجزئة مطلوب ويجب أن يكون أكبر من صفر.");
            else if (model.VolumeMl is > 0 && row.SizeMl >= model.VolumeMl.Value)
                AddModelErrorIfAbsent($"{prefix}.{nameof(ProductRetailPriceInput.SizeMl)}", "يجب أن يكون حجم التجزئة أصغر من حجم العبوة الأساسي.");
            else if (product != null && row.Id.HasValue && product.RetailPrices.Any(price =>
                         price.Id != row.Id.Value && price.SizeMl == row.SizeMl))
                AddModelErrorIfAbsent($"{prefix}.{nameof(ProductRetailPriceInput.SizeMl)}", "هذا الحجم موجود مسبقاً لهذا المنتج، حتى لو كان غير نشط.");

            if (row.Price <= 0)
                AddModelErrorIfAbsent($"{prefix}.{nameof(ProductRetailPriceInput.Price)}", "سعر التجزئة مطلوب ويجب أن يكون أكبر من صفر.");

            if (row.SizeMl <= 0)
                continue;

            if (sizeIndexes.TryGetValue(row.SizeMl, out var firstIndex))
            {
                AddModelErrorIfAbsent($"{prefix}.{nameof(ProductRetailPriceInput.SizeMl)}", "لا يمكن تكرار حجم التجزئة للمنتج نفسه.");
                AddModelErrorIfAbsent(
                    $"{fieldPrefix}{nameof(ProductVW.RetailPrices)}[{firstIndex}].{nameof(ProductRetailPriceInput.SizeMl)}",
                    "لا يمكن تكرار حجم التجزئة للمنتج نفسه.");
            }
            else
            {
                sizeIndexes[row.SizeMl] = index;
            }
        }

        var hasValidActiveRow = model.RetailPrices.Any(row =>
            row.IsActive &&
            row.SizeMl is > 0 and <= 1000000 &&
            row.Price is >= 0.01m and <= 1000000m &&
            model.VolumeMl is > 0 &&
            row.SizeMl < model.VolumeMl.Value &&
            model.RetailPrices.Count(candidate => candidate.SizeMl == row.SizeMl) == 1 &&
            (product == null || !row.Id.HasValue || knownIds.Contains(row.Id.Value)) &&
            (product == null || !row.Id.HasValue || product.RetailPrices.All(price =>
                price.Id == row.Id.Value || price.SizeMl != row.SizeMl)));

        if (!hasValidActiveRow)
        {
            AddModelErrorIfAbsent(
                $"{fieldPrefix}{nameof(ProductVW.RetailPrices)}",
                "يجب إضافة سعر تجزئة واحد على الأقل عند تفعيل البيع بالتجزئة.");
        }
    }

    private static void ValidateProductConfiguration(ProductVW model, bool isCreate, Product? existingProduct)
    {
        model.StockUnit = NormalizeStockUnit(model.StockUnit);

        if (model.StockUnit != "Ml" && model.IsRetailEnabled)
            throw new InvalidOperationException("لا يمكن تفعيل التجزئة إلا للمنتجات التي وحدتها مل.");

        if (model.StockUnit == "Ml")
        {
            if (model.VolumeMl is not > 0)
                throw new InvalidOperationException("حجم العبوة الأساسي مطلوب للمنتجات التي تُدار بالمل.");

            if (!isCreate &&
                existingProduct != null &&
                existingProduct.Stockquantity > 0 &&
                !string.Equals(existingProduct.StockUnit, model.StockUnit, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("لا يمكن تغيير وحدة المخزون لمنتج لديه رصيد قائم. أنشئ منتجاً جديداً أو استخدم تسوية مخزون واضحة.");
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
            .ToList();
    }

    private async Task SyncRetailPricesAsync(Product product, ProductVW model)
    {
        var isRetailEnabled = product.StockUnit == "Ml" && model.IsRetailEnabled;
        if (!isRetailEnabled)
        {
            foreach (var input in model.RetailPrices)
            {
                if (!input.Id.HasValue)
                    continue;

                var inputId = input.Id.Value;
                var existing = product.RetailPrices.FirstOrDefault(price => price.Id == inputId);
                if (existing != null)
                {
                    existing.SizeMl = input.SizeMl;
                    existing.Price = input.Price;
                    existing.IsActive = input.IsActive;
                }
            }

            return;
        }

        var submittedRows = ActiveRetailRows(model).ToList();
        var seenIds = submittedRows.Where(input => input.Id.HasValue).Select(input => input.Id!.Value).ToHashSet();

        var toRemove = product.RetailPrices.Where(p => !seenIds.Contains(p.Id)).ToList();

        if (toRemove.Any())
        {
            var removeIds = toRemove.Select(r => r.Id).ToList();
            var historicallyUsedIds = await _context.ProductRetailPrices
                .Where(p => removeIds.Contains(p.Id))
                .Where(p => p.Orderitems.Any() || p.SaleItems.Any())
                .Select(p => p.Id)
                .ToListAsync();

            foreach (var r in toRemove)
            {
                if (historicallyUsedIds.Contains(r.Id))
                {
                    r.IsActive = false;
                }
                else
                {
                    _context.ProductRetailPrices.Remove(r);
                }
            }
        }

        foreach (var input in submittedRows)
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

    private Task<List<int>> GetHistoricallyUsedRetailPriceIdsAsync(int productId) =>
        _context.ProductRetailPrices
            .Where(price => price.ProductId == productId)
            .Where(price => price.Orderitems.Any() || price.SaleItems.Any())
            .Select(price => price.Id)
            .ToListAsync();

    private static string NormalizeStockUnit(string? stockUnit) =>
        string.Equals(stockUnit, "Ml", StringComparison.OrdinalIgnoreCase) ? "Ml" : "Piece";

}
