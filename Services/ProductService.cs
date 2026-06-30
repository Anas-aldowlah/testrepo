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

    public Task<int> NextCounter()
    {
        int nextId = _context.Products.Any() ? _context.Products.Max(p => p.Id) + 1 : 1;
        return Task.FromResult(nextId);
    }


    public Task<IEnumerable<Product>> GetAllProductsAsync()
    {
        return Task.FromResult(_context.Products.AsEnumerable());
    }

    public Task<Product?> GetProductByIdAsync(int id)
    {
        return Task.FromResult(_context.Products.FirstOrDefault(p => p.Id == id));
    }

    public Task<IEnumerable<Category>> GetCategoriesAsync()
    {
        return Task.FromResult(_context.Categories.AsEnumerable());
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
