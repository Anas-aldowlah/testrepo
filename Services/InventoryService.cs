using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public class InventoryService : IInventoryService
{
    private readonly NeondbContext _context;

    public InventoryService(NeondbContext context)
    {
        _context = context;
    }

    public int CalculateDeductionAmount(Product product, int quantity, int? retailSizeMl)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero.");

        var unitAmount = GetUnitAmount(product, retailSizeMl);
        checked
        {
            return quantity * unitAmount;
        }
    }

    public int GetAvailableSaleUnits(Product product, int? retailSizeMl)
    {
        if (product.Stockquantity <= 0)
            return 0;

        var unitAmount = GetUnitAmount(product, retailSizeMl);
        return product.Stockquantity / unitAmount;
    }

    public decimal GetUnitPrice(Product product, ProductRetailPrice? retailPrice)
    {
        return retailPrice?.Price ?? product.Price;
    }

    public async Task<ProductRetailPrice?> ValidateRetailPriceAsync(
        Product product,
        int? retailPriceId,
        CancellationToken cancellationToken = default)
    {
        if (!retailPriceId.HasValue)
            return null;

        if (!string.Equals(product.StockUnit, "Ml", StringComparison.OrdinalIgnoreCase) ||
            product.VolumeMl is not > 0 ||
            !product.IsRetailEnabled)
        {
            throw new InvalidOperationException("Retail sale is not enabled for this product.");
        }

        var retailPrice = await _context.ProductRetailPrices
            .AsNoTracking()
            .SingleOrDefaultAsync(
                price => price.Id == retailPriceId.Value &&
                         price.ProductId == product.Id &&
                         price.IsActive,
                cancellationToken)
            ?? throw new InvalidOperationException("The selected retail size is not available.");

        return retailPrice;
    }

    public async Task DeductStockAsync(
        int productId,
        int amount,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Stock deduction amount must be greater than zero.");

        var affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE products SET stockquantity = stockquantity - {amount} WHERE id = {productId} AND stockquantity >= {amount}",
            cancellationToken);

        if (affectedRows != 1)
            throw new InvalidOperationException("The requested quantity is no longer available.");
    }

    public async Task RestoreStockAsync(
        int productId,
        int amount,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Stock restore amount must be greater than zero.");

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE products SET stockquantity = stockquantity + {amount} WHERE id = {productId}",
            cancellationToken);
    }

    private static int GetUnitAmount(Product product, int? retailSizeMl)
    {
        if (retailSizeMl.HasValue)
        {
            if (!string.Equals(product.StockUnit, "Ml", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Retail sizes can only be used with ml products.");

            if (retailSizeMl.Value <= 0)
                throw new InvalidOperationException("Retail size must be greater than zero.");

            return retailSizeMl.Value;
        }

        if (string.Equals(product.StockUnit, "Ml", StringComparison.OrdinalIgnoreCase))
        {
            if (product.VolumeMl is not > 0)
                throw new InvalidOperationException("Bottle volume must be configured for ml products.");

            return product.VolumeMl.Value;
        }

        return 1;
    }
}
