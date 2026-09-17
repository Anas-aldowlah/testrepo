using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;
using YAGOT_2._0.Services.Promotions;

namespace YAGOT_2._0.Controllers;

[ServiceFilter(typeof(SiteStatusFilter))]
public class OffersController : Controller
{
    private readonly ProductService _productService;
    private readonly IPromotionEngine _promotionEngine;

    public OffersController(ProductService productService, IPromotionEngine promotionEngine)
    {
        _productService = productService;
        _promotionEngine = promotionEngine;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? q,
        int? categoryId,
        string? promoType,
        string? sort = "priority",
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var selectedCategoryId = categoryId.HasValue && categoryId.Value > 0 ? categoryId : null;
        var allProducts = await _productService.GetAllProductsAsync(selectedCategoryId, inStockOnly: false, search: q, cancellationToken: cancellationToken);
        var categories = await _productService.GetCategoriesAsync(cancellationToken);

        // ProductService only attaches promotions that are active at the current UTC instant.
        var query = allProducts.Where(p => p.HasPromotion);

        // Search Filter
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                     (p.Category != null && p.Category.Name.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        // Category Filter
        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.Categoryid == categoryId.Value);
        }

        // Promo Type Filter
        if (!string.IsNullOrWhiteSpace(promoType))
        {
            query = query.Where(p => string.Equals(p.PromotionType, promoType, StringComparison.OrdinalIgnoreCase));
        }

        // Sorting
        query = sort switch
        {
            "newest" => query.OrderByDescending(p => p.Createdat),
            "discount_desc" => query.OrderByDescending(p => p.DiscountAmount),
            "price_asc" => query.OrderBy(p => p.FinalPrice),
            "price_desc" => query.OrderByDescending(p => p.FinalPrice),
            _ => query.OrderBy(p => p.PromotionPriority ?? int.MaxValue).ThenByDescending(p => p.DiscountAmount)
        };

        var filteredList = query.ToList();
        int totalItems = filteredList.Count;
        int pageSize = 12;
        int currentPage = Math.Max(1, page);

        var pageItems = filteredList
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var activePromos = await _promotionEngine.GetActivePromotionsAsync(false, cancellationToken);
        ViewBag.ActiveSpendPromotions = activePromos.Where(p => p.PromotionType == "SpendAmount").ToList();

        var viewModel = new OffersViewModel
        {
            Products = pageItems,
            Categories = categories,
            SearchQuery = q,
            CategoryId = categoryId,
            PromoType = promoType,
            Sort = sort,
            CurrentPage = currentPage,
            PageSize = pageSize,
            TotalItems = totalItems
        };

        return View(viewModel);
    }
}
