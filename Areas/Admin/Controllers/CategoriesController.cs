using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services;
using YAGOT_2._0.Services.Caching;

namespace YAGOT_2._0.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Developer")]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public class CategoriesController : Controller
{
    private const string DefaultCategoryImageUrl = "/images/categories/category_8428362.png";

    private readonly NeondbContext _context;
    private readonly CategoryServer _categoryService;
    private readonly Image _ImageServes;
    private readonly ProductCatalogService _catalogService;
    private readonly ICacheInvalidationService _invalidationService;
    private readonly ICapabilityEvaluator _capabilityEvaluator;

    public CategoriesController(
        NeondbContext context,
        CategoryServer categoryService,
        Image imageServes,
        ProductCatalogService catalogService,
        ICacheInvalidationService invalidationService,
        ICapabilityEvaluator capabilityEvaluator)
    {
        _context = context;
        _categoryService = categoryService;
        _ImageServes = imageServes;
        _catalogService = catalogService;
        _invalidationService = invalidationService;
        _capabilityEvaluator = capabilityEvaluator;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
    {
        if (!IsEnabled(CapabilityFeatureCodes.CategoryView))
        {
            return Forbid();
        }

        var categoryQuery = _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new AdminCategoryListItemViewModel
            {
                Category = c,
                ProductCount = _context.Products.Count(p => p.Categoryid == c.Id)
            });

        var pagedCategories = await PagedResult<AdminCategoryListItemViewModel>.CreateAsync(categoryQuery, page, pageSize);
        var model = new AdminCategoriesIndexViewModel
        {
            Categories = pagedCategories,
            TotalCategories = pagedCategories.TotalItems,
            TotalProducts = await _context.Products.CountAsync()
        };
        
        return View(model);
    }

    public IActionResult Create()
    {
        if (!IsEnabled(CapabilityFeatureCodes.CategoryCreate))
        {
            return Forbid();
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryVW categoryVW)
    {
        if (!IsEnabled(CapabilityFeatureCodes.CategoryCreate) ||
            HasImageMutation(categoryVW) && !IsEnabled(CapabilityFeatureCodes.CategoryImages))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(categoryVW);
        }

        string? imageUrl = await _ImageServes.UploadImage(categoryVW.ImageFile, "categories");

        var model = new Category
        {
            Name = categoryVW.Name,
            Description = categoryVW.Description,
            Imageurl = imageUrl != null ? GetCategoryImageUrl(imageUrl) : DefaultCategoryImageUrl,
        };

        await _context.Categories.AddAsync(model);
        await _context.SaveChangesAsync();
        _invalidationService.InvalidateCatalogMetadata();
        _invalidationService.InvalidateCategoriesList();
        _invalidationService.InvalidateHomeShowcase();
        TempData["Success"] = "تمت إضافة التصنيف بنجاح.";
        return RedirectToAction(nameof(Index));
    }
    
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsEnabled(CapabilityFeatureCodes.CategoryEdit))
        {
            return Forbid();
        }

        var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (category == null)
        {
            return NotFound();
        }
        CategoryVW categoryVW = new CategoryVW
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            Existingimage = category.Imageurl
        };

        return View(categoryVW);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryVW categoryVW)
    {
        if (!IsEnabled(CapabilityFeatureCodes.CategoryEdit) ||
            HasImageMutation(categoryVW) && !IsEnabled(CapabilityFeatureCodes.CategoryImages))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(categoryVW);
        }

        var category = await _categoryService.GetCategoryByID(categoryVW.Id);
        if (category==null) return NotFound();
        var previousImageUrl = category.Imageurl;
        string? uploadedImageUrl = null;
        var imageReferenceSaved = false;
        try
        {
            category.Imageurl = previousImageUrl;
            if (categoryVW.ImageFile != null && categoryVW.ImageFile.Length > 0)
            {
                uploadedImageUrl = await _ImageServes.UpdateImage(
                    categoryVW.ImageFile,
                    "categories",
                    previousImageUrl ?? string.Empty);

                if (uploadedImageUrl != null)
                    category.Imageurl = uploadedImageUrl;
            }
            category.Name = categoryVW.Name;
            category.Description = categoryVW.Description;

            _context.Update(category);
            await _context.SaveChangesAsync();
            imageReferenceSaved = true;

            if (uploadedImageUrl != null &&
                !string.Equals(previousImageUrl, DefaultCategoryImageUrl, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(previousImageUrl, DefaultCategoryImageUrl.TrimStart('/'), StringComparison.OrdinalIgnoreCase))
            {
                _ImageServes.DeleteImage("categories", previousImageUrl);
            }

            _invalidationService.InvalidateCatalogMetadata();
            _invalidationService.InvalidateCategoriesList();
            _invalidationService.InvalidateHomeShowcase();
            TempData["Success"] = "تم حفظ تعديلات التصنيف بنجاح.";
            return RedirectToAction(nameof(Index));
        }
        finally
        {
            if (!imageReferenceSaved && uploadedImageUrl != null)
                _ImageServes.DeleteImage("categories", uploadedImageUrl);
        }

    }

    private static string GetCategoryImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return DefaultCategoryImageUrl;
        }

        return $"/images/categories/{Path.GetFileName(imageUrl)}";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsEnabled(CapabilityFeatureCodes.CategoryDelete))
        {
            return Forbid();
        }

        var category = _context.Categories.FirstOrDefault(c => c.Id == id);
        if (category != null)
        {
            // لا نحذف التصنيف إذا كانت هناك منتجات مرتبطة به
            var hasProducts = _context.Products.Any(p => p.Categoryid == id);
            if (hasProducts)
            {
                TempData["Error"] = "لا يمكن حذف التصنيف لأنه يحتوي على منتجات";
                return RedirectToAction(nameof(Index));
            }
            var previousImageUrl = category.Imageurl;
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            if (!string.Equals(previousImageUrl, DefaultCategoryImageUrl, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(previousImageUrl, DefaultCategoryImageUrl.TrimStart('/'), StringComparison.OrdinalIgnoreCase))
            {
                _ImageServes.DeleteImage("categories", previousImageUrl);
            }
            _invalidationService.InvalidateCatalogMetadata();
            _invalidationService.InvalidateCategoriesList();
            _invalidationService.InvalidateHomeShowcase();
            TempData["Success"] = "تم حذف التصنيف بنجاح.";

        }
        return RedirectToAction(nameof(Index));
    }

    private bool IsEnabled(string featureCode) => _capabilityEvaluator.IsFeatureEnabled(featureCode);

    private static bool HasImageMutation(CategoryVW category) =>
        category.ImageFile is { Length: > 0 };
}
