using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services.Promotions;

namespace YAGOT_2._0.Services;

public class ProductService 
{
    private readonly NeondbContext _context;
    private readonly IPromotionEngine _promotionEngine;

    public ProductService(NeondbContext context, IPromotionEngine promotionEngine)
    {
        _context = context;
        _promotionEngine = promotionEngine;
    }

    public async Task<IEnumerable<Product>> GetAllProductsAsync(
        int? categoryId = null,
        bool inStockOnly = false,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.RetailPrices)
            .AsQueryable();

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.Categoryid == categoryId.Value);
        }

        if (inStockOnly)
        {
            query = query.Where(p => p.Stockquantity > 0);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern) ||
                (p.Category != null && EF.Functions.ILike(p.Category.Name, pattern)));
        }

        var products = await query.ToListAsync(cancellationToken);
        await AttachPromotionsDataAsync(products, cancellationToken);
        return products;
    }

    public async Task<Product?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.RetailPrices)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product != null)
        {
            await AttachPromotionDataAsync(product, null, cancellationToken);
        }

        return product;
    }

    public async Task<IEnumerable<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Evaluates and attaches active promotion data to a single Product dynamically.
    /// </summary>
    public async Task AttachPromotionDataAsync(
        Product product,
        List<Promotion>? activePromotions = null,
        CancellationToken cancellationToken = default)
    {
        if (product == null) return;
        var promoResult = await _promotionEngine.CalculateProductDiscountAsync(product, 1, null, activePromotions, cancellationToken);
        if (promoResult.HasPromotion)
        {
            product.HasPromotion = true;
            product.PromotionId = promoResult.PromotionId;
            product.PromotionTitle = promoResult.PromotionTitle;
            product.PromotionType = promoResult.PromotionType;
            product.DiscountAmount = promoResult.DiscountAmount;
            product.FreeQuantity = promoResult.ConfiguredFreeQuantity;
            product.BuyQuantity = promoResult.BuyQuantity;
            product.PromotionStartDate = promoResult.StartDate;
            product.PromotionEndDate = promoResult.EndDate;
            product.PromotionPriority = promoResult.Priority;
            product.PromotionDescription = promoResult.Description;
            product.PromotionBannerImage = promoResult.BannerImage;
            product.PromoDiscountValue = promoResult.DiscountValue;
            product.OfferPrice = promoResult.OfferPrice;
            product.MinimumAmount = promoResult.MinimumAmount;
        }
        else
        {
            product.HasPromotion = false;
            product.DiscountAmount = 0;
        }
    }

    /// <summary>
    /// Batch-evaluates and attaches promotion data to a collection of Product objects.
    /// </summary>
    public async Task AttachPromotionsDataAsync(
        IEnumerable<Product> products,
        CancellationToken cancellationToken = default)
    {
        if (products == null || !products.Any()) return;

        var activePromotions = await _promotionEngine.GetActivePromotionsAsync(false, cancellationToken);
        if (!activePromotions.Any())
        {
            foreach (var p in products)
            {
                p.HasPromotion = false;
                p.DiscountAmount = 0;
            }
            return;
        }

        foreach (var product in products)
        {
            await AttachPromotionDataAsync(product, activePromotions, cancellationToken);
        }
    }

    //public Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId)
    //{
    //    return Task.FromResult(FakeDb.Products.Where(p => p.CategoryId == categoryId));
    //}

    //public Task<IEnumerable<Product>> SearchProductsAsync(string query)
    //{
    //    if (string.IsNullOrWhiteSpace(query)) return GetAllProductsAsync();

    //    var results = FakeDb.Products.Where(p => 
    //        p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || 
    //        p.Description.Contains(query, StringComparison.OrdinalIgnoreCase));

    //    return Task.FromResult(results);
    //}

    //public Task AddProductAsync(Product product)
    //{
    //    product.Id = FakeDb.NextProductId();
    //    product.CreatedAt = DateTime.UtcNow;
    //    FakeDb.Products.Add(product);
    //    return Task.CompletedTask;
    //}

    //public Task UpdateProductAsync(Product product)
    //{
    //    var existing = FakeDb.Products.FirstOrDefault(p => p.Id == product.Id);
    //    if (existing != null)
    //    {
    //        existing.Name = product.Name;
    //        existing.Description = product.Description;
    //        existing.Price = product.Price;
    //        existing.StockQuantity = product.StockQuantity;
    //        existing.CategoryId = product.CategoryId;
    //        if (!string.IsNullOrEmpty(product.ImageUrl))
    //        {
    //            existing.ImageUrl = product.ImageUrl;
    //        }
    //    }
    //    return Task.CompletedTask;
    //}

    //public Task DeleteProductAsync(int id)
    //{
    //    var product = FakeDb.Products.FirstOrDefault(p => p.Id == id);
    //    if (product != null) FakeDb.Products.Remove(product);
    //    return Task.CompletedTask;
    //}
}
