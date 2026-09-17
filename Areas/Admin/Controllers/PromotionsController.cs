using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services;
using YAGOT_2._0.Services.Promotions;

namespace YAGOT_2._0.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Developer")]
    [ServiceFilter(typeof(SiteStatusFilterAdmin))]
    public class PromotionsController : Controller
    {
        private readonly NeondbContext _context;
        private readonly Image _imageService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IPromotionEngine _promotionEngine;

        public PromotionsController(
            NeondbContext context,
            Image imageService,
            IWebHostEnvironment webHostEnvironment,
            IPromotionEngine promotionEngine)
        {
            _context = context;
            _imageService = imageService;
            _webHostEnvironment = webHostEnvironment;
            _promotionEngine = promotionEngine;
        }

        // GET: Admin/Promotions
        public async Task<IActionResult> Index(string? search, string? type, string? status, int page = 1)
        {
            int pageSize = 10;
            var utcNow = DateTimeOffset.UtcNow;

            var summary = await _context.Promotions
                .AsNoTracking()
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Total = group.Count(),
                    Active = group.Count(p => p.IsActive && p.StartDate <= utcNow && utcNow < p.EndDate),
                    Upcoming = group.Count(p => p.IsActive && p.StartDate > utcNow),
                    Expired = group.Count(p => p.IsActive && p.EndDate <= utcNow),
                    Disabled = group.Count(p => !p.IsActive)
                })
                .FirstOrDefaultAsync();

            var totalPromotions = summary?.Total ?? 0;
            var activePromotions = summary?.Active ?? 0;
            var upcomingPromotions = summary?.Upcoming ?? 0;
            var expiredPromotions = summary?.Expired ?? 0;
            var disabledPromotions = summary?.Disabled ?? 0;

            var allPromotionsQuery = _context.Promotions
                .AsNoTracking()
                .Include(p => p.PromotionProducts)
                .Include(p => p.PromotionCategories)
                .AsQueryable();

            var filteredQuery = allPromotionsQuery;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLower();
                filteredQuery = filteredQuery.Where(p => p.Title.ToLower().Contains(searchLower) ||
                                                        (p.Description != null && p.Description.ToLower().Contains(searchLower)));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                filteredQuery = filteredQuery.Where(p => p.PromotionType == type);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                switch (status.ToLower())
                {
                    case "active":
                        filteredQuery = filteredQuery.Where(p => p.IsActive && p.StartDate <= utcNow && utcNow < p.EndDate);
                        break;
                    case "upcoming":
                        filteredQuery = filteredQuery.Where(p => p.IsActive && p.StartDate > utcNow);
                        break;
                    case "expired":
                        filteredQuery = filteredQuery.Where(p => p.IsActive && p.EndDate <= utcNow);
                        break;
                    case "disabled":
                        filteredQuery = filteredQuery.Where(p => !p.IsActive);
                        break;
                }
            }

            var totalItems = await filteredQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var items = await filteredQuery
                .OrderBy(p => p.Priority)
                .ThenByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var listItems = items.Select(p => new PromotionListItemVM
            {
                Id = p.Id,
                Title = p.Title,
                PromotionType = p.PromotionType,
                DiscountValue = p.DiscountValue,
                SpendDiscountType = p.SpendDiscountType,
                OfferPrice = p.OfferPrice,
                BuyQuantity = p.BuyQuantity,
                FreeQuantity = p.FreeQuantity,
                StartDate = PromotionTime.UtcToYemenLocal(p.StartDate),
                EndDate = PromotionTime.UtcToYemenLocal(p.EndDate),
                IsActive = p.IsActive,
                Priority = p.Priority,
                BannerImage = p.BannerImage,
                CreatedAt = p.CreatedAt,
                StatusKey = PromotionTime.GetStatusKey(p, utcNow),
                TargetSummary = GetTargetSummary(p)
            }).ToList();

            var viewModel = new PromotionListViewModel
            {
                Promotions = listItems,
                TotalPromotions = totalPromotions,
                ActivePromotions = activePromotions,
                UpcomingPromotions = upcomingPromotions,
                ExpiredPromotions = expiredPromotions,
                DisabledPromotions = disabledPromotions,
                Search = search,
                TypeFilter = type,
                StatusFilter = status,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalItems = totalItems
            };

            return View(viewModel);
        }

        // GET: Admin/Promotions/Create
        public async Task<IActionResult> Create()
        {
            var yemenNow = PromotionTime.UtcToYemenLocal(DateTimeOffset.UtcNow);
            var model = new PromotionFormVM
            {
                StartDate = yemenNow,
                EndDate = yemenNow.AddDays(7),
                IsActive = true,
                Priority = 10,
                TargetType = "All",
                AvailableProducts = await GetProductOptionsAsync(),
                AvailableCategories = await GetCategoryOptionsAsync()
            };

            return View(model);
        }

        // POST: Admin/Promotions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Create(PromotionFormVM model)
        {
            List<int> validProductIds = new();
            List<int> validCategoryIds = new();

            if (model.TargetType == "Products")
            {
                var requestedIds = model.SelectedProductIds?.Distinct().ToList() ?? new List<int>();
                if (requestedIds.Count == 0)
                {
                    ModelState.AddModelError(nameof(model.SelectedProductIds), "يجب اختيار منتج واحد على الأقل عند اختيار المنتجات كنطاق مستهدف");
                }
                else
                {
                    validProductIds = await _context.Products
                        .AsNoTracking()
                        .Where(p => requestedIds.Contains(p.Id))
                        .Select(p => p.Id)
                        .ToListAsync();

                    if (validProductIds.Count == 0)
                    {
                        ModelState.AddModelError(nameof(model.SelectedProductIds), "المنتجات المحددة غير صالحة أو غير موجودة في قاعدة البيانات");
                    }
                }
            }
            else if (model.TargetType == "Categories")
            {
                var requestedIds = model.SelectedCategoryIds?.Distinct().ToList() ?? new List<int>();
                if (requestedIds.Count == 0)
                {
                    ModelState.AddModelError(nameof(model.SelectedCategoryIds), "يجب اختيار تصنيف واحد على الأقل عند اختيار التصنيفات كنطاق مستهدف");
                }
                else
                {
                    validCategoryIds = await _context.Categories
                        .AsNoTracking()
                        .Where(c => requestedIds.Contains(c.Id))
                        .Select(c => c.Id)
                        .ToListAsync();

                    if (validCategoryIds.Count == 0)
                    {
                        ModelState.AddModelError(nameof(model.SelectedCategoryIds), "التصنيفات المحددة غير صالحة أو غير موجودة في قاعدة البيانات");
                    }
                }
            }

            if (ModelState.IsValid)
            {
                string? bannerUrl = null;
                if (model.BannerFile != null)
                {
                    string? filename = await _imageService.UploadImage(model.BannerFile, "banners");
                    if (filename != null)
                    {
                        bannerUrl = "/images/banners/" + filename;
                    }
                }

                var promotion = new Promotion
                {
                    Title = model.Title.Trim(),
                    Description = model.Description?.Trim(),
                    PromotionType = model.PromotionType,
                    TargetType = model.TargetType,
                    DiscountValue = model.DiscountValue ?? 0m,
                    SpendDiscountType = model.SpendDiscountType,
                    MinimumAmount = model.MinimumAmount,
                    BuyQuantity = model.BuyQuantity,
                    FreeQuantity = model.FreeQuantity,
                    OfferPrice = model.OfferPrice,
                    StartDate = PromotionTime.YemenLocalToUtc(model.StartDate),
                    EndDate = PromotionTime.YemenLocalToUtc(model.EndDate),
                    IsActive = model.IsActive,
                    Priority = model.Priority,
                    CanBeCombined = model.CanBeCombined,
                    BannerImage = bannerUrl,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Promotions.Add(promotion);
                await _context.SaveChangesAsync();

                if (model.TargetType == "Products" && validProductIds.Count > 0)
                {
                    foreach (var productId in validProductIds)
                    {
                        _context.PromotionProducts.Add(new PromotionProduct
                        {
                            PromotionId = promotion.Id,
                            ProductId = productId,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    await _context.SaveChangesAsync();
                }
                else if (model.TargetType == "Categories" && validCategoryIds.Count > 0)
                {
                    foreach (var categoryId in validCategoryIds)
                    {
                        _context.PromotionCategories.Add(new PromotionCategory
                        {
                            PromotionId = promotion.Id,
                            CategoryId = categoryId,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                _promotionEngine.InvalidateActivePromotionsCache();

                TempData["SuccessMessage"] = "تم إنشاء العرض بنجاح";
                return RedirectToAction(nameof(Index));
            }

            model.AvailableProducts = await GetProductOptionsAsync(model.SelectedProductIds);
            model.AvailableCategories = await GetCategoryOptionsAsync(model.SelectedCategoryIds);
            return View(model);
        }

        // GET: Admin/Promotions/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var promotion = await _context.Promotions
                .AsNoTracking()
                .Include(p => p.PromotionProducts)
                    .ThenInclude(pp => pp.Product)
                .Include(p => p.PromotionCategories)
                    .ThenInclude(pc => pc.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (promotion == null)
            {
                return NotFound();
            }

            var linkedProducts = promotion.PromotionProducts.Select(pp => pp.Product).ToList();
            var linkedCategories = promotion.PromotionCategories.Select(pc => pc.Category).ToList();

            var viewModel = new PromotionDetailsVM
            {
                Promotion = promotion,
                StartDateLocal = PromotionTime.UtcToYemenLocal(promotion.StartDate),
                EndDateLocal = PromotionTime.UtcToYemenLocal(promotion.EndDate),
                StatusKey = PromotionTime.GetStatusKey(promotion, DateTimeOffset.UtcNow),
                TargetSummary = GetTargetSummary(promotion),
                LinkedProducts = linkedProducts,
                LinkedCategories = linkedCategories
            };

            return View(viewModel);
        }

        // GET: Admin/Promotions/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var promotion = await _context.Promotions
                .AsNoTracking()
                .Include(p => p.PromotionProducts)
                .Include(p => p.PromotionCategories)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (promotion == null)
            {
                return NotFound();
            }

            string targetType = !string.IsNullOrEmpty(promotion.TargetType) ? promotion.TargetType : "All";
            var selectedProductIds = promotion.PromotionProducts.Select(pp => pp.ProductId).ToList();
            var selectedCategoryIds = promotion.PromotionCategories.Select(pc => pc.CategoryId).ToList();

            if (string.IsNullOrEmpty(promotion.TargetType))
            {
                if (selectedProductIds.Count > 0)
                {
                    targetType = "Products";
                }
                else if (selectedCategoryIds.Count > 0)
                {
                    targetType = "Categories";
                }
            }

            var model = new PromotionFormVM
            {
                Id = promotion.Id,
                Title = promotion.Title,
                Description = promotion.Description,
                PromotionType = promotion.PromotionType,
                DiscountValue = promotion.DiscountValue,
                SpendDiscountType = promotion.SpendDiscountType,
                MinimumAmount = promotion.MinimumAmount,
                BuyQuantity = promotion.BuyQuantity,
                FreeQuantity = promotion.FreeQuantity,
                OfferPrice = promotion.OfferPrice,
                StartDate = PromotionTime.UtcToYemenLocal(promotion.StartDate),
                EndDate = PromotionTime.UtcToYemenLocal(promotion.EndDate),
                IsActive = promotion.IsActive,
                Priority = promotion.Priority,
                CanBeCombined = promotion.CanBeCombined,
                TargetType = targetType,
                ExistingBanner = promotion.BannerImage,
                SelectedProductIds = selectedProductIds,
                SelectedCategoryIds = selectedCategoryIds,
                AvailableProducts = await GetProductOptionsAsync(selectedProductIds),
                AvailableCategories = await GetCategoryOptionsAsync(selectedCategoryIds)
            };

            return View(model);
        }

        // POST: Admin/Promotions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Edit(int id, PromotionFormVM model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            List<int> validProductIds = new();
            List<int> validCategoryIds = new();

            if (model.TargetType == "Products")
            {
                var requestedIds = model.SelectedProductIds?.Distinct().ToList() ?? new List<int>();
                if (requestedIds.Count == 0)
                {
                    ModelState.AddModelError(nameof(model.SelectedProductIds), "يجب اختيار منتج واحد على الأقل عند اختيار المنتجات كنطاق مستهدف");
                }
                else
                {
                    validProductIds = await _context.Products
                        .AsNoTracking()
                        .Where(p => requestedIds.Contains(p.Id))
                        .Select(p => p.Id)
                        .ToListAsync();

                    if (validProductIds.Count == 0)
                    {
                        ModelState.AddModelError(nameof(model.SelectedProductIds), "المنتجات المحددة غير صالحة أو غير موجودة في قاعدة البيانات");
                    }
                }
            }
            else if (model.TargetType == "Categories")
            {
                var requestedIds = model.SelectedCategoryIds?.Distinct().ToList() ?? new List<int>();
                if (requestedIds.Count == 0)
                {
                    ModelState.AddModelError(nameof(model.SelectedCategoryIds), "يجب اختيار تصنيف واحد على الأقل عند اختيار التصنيفات كنطاق مستهدف");
                }
                else
                {
                    validCategoryIds = await _context.Categories
                        .AsNoTracking()
                        .Where(c => requestedIds.Contains(c.Id))
                        .Select(c => c.Id)
                        .ToListAsync();

                    if (validCategoryIds.Count == 0)
                    {
                        ModelState.AddModelError(nameof(model.SelectedCategoryIds), "التصنيفات المحددة غير صالحة أو غير موجودة في قاعدة البيانات");
                    }
                }
            }

            if (ModelState.IsValid)
            {
                var promotion = await _context.Promotions
                    .Include(p => p.PromotionProducts)
                    .Include(p => p.PromotionCategories)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (promotion == null)
                {
                    return NotFound();
                }

                string? oldBannerToCleanup = null;
                if (model.BannerFile != null)
                {
                    string? filename = await _imageService.UploadImage(model.BannerFile, "banners");
                    if (filename != null)
                    {
                        oldBannerToCleanup = promotion.BannerImage;
                        promotion.BannerImage = "/images/banners/" + filename;
                    }
                }

                promotion.Title = model.Title.Trim();
                promotion.Description = model.Description?.Trim();
                promotion.PromotionType = model.PromotionType;
                promotion.TargetType = model.TargetType;
                promotion.DiscountValue = model.DiscountValue ?? 0m;
                promotion.SpendDiscountType = model.SpendDiscountType;
                promotion.MinimumAmount = model.MinimumAmount;
                promotion.BuyQuantity = model.BuyQuantity;
                promotion.FreeQuantity = model.FreeQuantity;
                promotion.OfferPrice = model.OfferPrice;
                promotion.StartDate = PromotionTime.YemenLocalToUtc(model.StartDate);
                promotion.EndDate = PromotionTime.YemenLocalToUtc(model.EndDate);
                promotion.IsActive = model.IsActive;
                promotion.Priority = model.Priority;
                promotion.CanBeCombined = model.CanBeCombined;
                promotion.UpdatedAt = DateTime.UtcNow;

                _context.PromotionProducts.RemoveRange(promotion.PromotionProducts);
                _context.PromotionCategories.RemoveRange(promotion.PromotionCategories);

                if (model.TargetType == "Products" && validProductIds.Count > 0)
                {
                    foreach (var productId in validProductIds)
                    {
                        _context.PromotionProducts.Add(new PromotionProduct
                        {
                            PromotionId = promotion.Id,
                            ProductId = productId,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                else if (model.TargetType == "Categories" && validCategoryIds.Count > 0)
                {
                    foreach (var categoryId in validCategoryIds)
                    {
                        _context.PromotionCategories.Add(new PromotionCategory
                        {
                            PromotionId = promotion.Id,
                            CategoryId = categoryId,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();
                _promotionEngine.InvalidateActivePromotionsCache();

                if (!string.IsNullOrEmpty(oldBannerToCleanup) && oldBannerToCleanup.StartsWith("/images/banners/"))
                {
                    try
                    {
                        var relativePath = oldBannerToCleanup.TrimStart('/');
                        var oldFullPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(oldFullPath))
                        {
                            System.IO.File.Delete(oldFullPath);
                        }
                    }
                    catch
                    {
                        // Safe suppression of file cleanup errors
                    }
                }

                TempData["SuccessMessage"] = "تم تحديث العرض بنجاح";
                return RedirectToAction(nameof(Index));
            }

            model.AvailableProducts = await GetProductOptionsAsync(model.SelectedProductIds);
            model.AvailableCategories = await GetCategoryOptionsAsync(model.SelectedCategoryIds);
            return View(model);
        }

        // POST: Admin/Promotions/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var promo = await _context.Promotions.FindAsync(id);
            if (promo == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
                {
                    return NotFound(new { success = false, message = "العرض غير موجود" });
                }
                return NotFound();
            }

            promo.IsActive = !promo.IsActive;
            promo.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _promotionEngine.InvalidateActivePromotionsCache();

            var message = promo.IsActive ? "تم تفعيل العرض بنجاح" : "تم إيقاف العرض بنجاح";

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new
                {
                    success = true,
                    isActive = promo.IsActive,
                    statusKey = PromotionTime.GetStatusKey(promo, DateTimeOffset.UtcNow),
                    message = message
                });
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Promotions/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var promotion = await _context.Promotions
                .AsNoTracking()
                .Include(p => p.PromotionProducts)
                    .ThenInclude(pp => pp.Product)
                .Include(p => p.PromotionCategories)
                    .ThenInclude(pc => pc.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (promotion == null)
            {
                return NotFound();
            }

            var viewModel = new PromotionDetailsVM
            {
                Promotion = promotion,
                StartDateLocal = PromotionTime.UtcToYemenLocal(promotion.StartDate),
                EndDateLocal = PromotionTime.UtcToYemenLocal(promotion.EndDate),
                StatusKey = PromotionTime.GetStatusKey(promotion, DateTimeOffset.UtcNow),
                TargetSummary = GetTargetSummary(promotion),
                LinkedProducts = promotion.PromotionProducts.Select(pp => pp.Product).ToList(),
                LinkedCategories = promotion.PromotionCategories.Select(pc => pc.Category).ToList()
            };

            return View(viewModel);
        }

        // POST: Admin/Promotions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion != null)
            {
                var bannerToDelete = promotion.BannerImage;
                _context.Promotions.Remove(promotion);
                await _context.SaveChangesAsync();
                _promotionEngine.InvalidateActivePromotionsCache();

                if (!string.IsNullOrEmpty(bannerToDelete) && bannerToDelete.StartsWith("/images/banners/"))
                {
                    try
                    {
                        var relativePath = bannerToDelete.TrimStart('/');
                        var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(fullPath))
                        {
                            System.IO.File.Delete(fullPath);
                        }
                    }
                    catch
                    {
                        // Safe suppression of file cleanup errors
                    }
                }

                TempData["SuccessMessage"] = "تم حذف العرض بنجاح";
            }

            return RedirectToAction(nameof(Index));
        }

        // Helper Methods
        private static string GetTargetSummary(Promotion promotion)
        {
            if (string.Equals(promotion.TargetType, "Products", StringComparison.OrdinalIgnoreCase))
            {
                int count = promotion.PromotionProducts?.Count ?? 0;
                return count > 0 ? $"منتجات محددة ({count})" : "منتجات محددة (0 - لا يوجد منتجات)";
            }
            if (string.Equals(promotion.TargetType, "Categories", StringComparison.OrdinalIgnoreCase))
            {
                int count = promotion.PromotionCategories?.Count ?? 0;
                return count > 0 ? $"تصنيفات محددة ({count})" : "تصنيفات محددة (0 - لا يوجد تصنيفات)";
            }
            if (promotion.PromotionProducts != null && promotion.PromotionProducts.Count > 0)
            {
                return $"منتجات محددة ({promotion.PromotionProducts.Count})";
            }
            if (promotion.PromotionCategories != null && promotion.PromotionCategories.Count > 0)
            {
                return $"تصنيفات محددة ({promotion.PromotionCategories.Count})";
            }
            return "المتجر بالكامل";
        }

        private async Task<List<ProductSelectOption>> GetProductOptionsAsync(List<int>? selectedIds = null)
        {
            selectedIds ??= new List<int>();
            var products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .OrderBy(p => p.Name)
                .ToListAsync();

            return products.Select(p => new ProductSelectOption
            {
                Id = p.Id,
                Name = p.Name,
                Brand = p.Brand,
                Price = p.Price,
                CategoryName = p.Category?.Name,
                ImageUrl = p.Imageurl,
                IsSelected = selectedIds.Contains(p.Id)
            }).ToList();
        }

        private async Task<List<CategorySelectOption>> GetCategoryOptionsAsync(List<int>? selectedIds = null)
        {
            selectedIds ??= new List<int>();
            var categories = await _context.Categories
                .AsNoTracking()
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return categories.Select(c =>
            {
                var img = !string.IsNullOrWhiteSpace(c.Imageurl)
                    ? c.Imageurl
                    : c.Products.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Imageurl))?.Imageurl;

                return new CategorySelectOption
                {
                    Id = c.Id,
                    Name = c.Name,
                    ImageUrl = img,
                    ProductCount = c.Products.Count,
                    IsSelected = selectedIds.Contains(c.Id)
                };
            }).ToList();
        }
    }
}
