using System.Linq.Expressions;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public static class ProductRetailAvailability
{
    public static readonly Expression<Func<Product, bool>> EffectiveRetailAvailable = product =>
        product.IsRetailEnabled &&
        product.StockUnit == "Ml" &&
        product.VolumeMl.HasValue &&
        product.VolumeMl.Value > 0 &&
        product.RetailPrices.Any(price =>
            price.IsActive &&
            price.SizeMl > 0 &&
            price.Price > 0 &&
            price.SizeMl < product.VolumeMl.Value);

    private static readonly Func<Product, bool> IsEffectiveRetailAvailable =
        EffectiveRetailAvailable.Compile();

    public static IQueryable<Product> WhereEffectiveRetailAvailability(
        this IQueryable<Product> query,
        bool available)
    {
        if (available)
            return query.Where(EffectiveRetailAvailable);

        var product = EffectiveRetailAvailable.Parameters[0];
        var unavailable = Expression.Lambda<Func<Product, bool>>(
            Expression.Not(EffectiveRetailAvailable.Body),
            product);
        return query.Where(unavailable);
    }

    public static bool IsAvailable(Product product) => IsEffectiveRetailAvailable(product);

    public static IReadOnlyList<ProductRetailPrice> GetCustomerUsablePrices(Product product)
    {
        if (!product.IsRetailEnabled ||
            product.StockUnit != "Ml" ||
            product.VolumeMl is not > 0)
        {
            return [];
        }

        return product.RetailPrices
            .Where(price =>
                price.IsActive &&
                price.SizeMl > 0 &&
                price.Price > 0 &&
                price.SizeMl < product.VolumeMl.Value)
            .OrderBy(price => price.SizeMl)
            .ToList();
    }
}
