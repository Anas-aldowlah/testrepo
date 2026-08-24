using Microsoft.EntityFrameworkCore;
using System.Linq;

using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public class ProductService 
{
    private readonly NeondbContext _context;

    public ProductService(NeondbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Product>> GetAllProductsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
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
