using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public interface IInventoryService
{
    int CalculateDeductionAmount(Product product, int quantity, int? retailSizeMl);
    int GetAvailableSaleUnits(Product product, int? retailSizeMl);
    decimal GetUnitPrice(Product product, ProductRetailPrice? retailPrice);
    Task<ProductRetailPrice?> ValidateRetailPriceAsync(Product product, int? retailPriceId, CancellationToken cancellationToken = default);
    Task DeductStockAsync(int productId, int amount, CancellationToken cancellationToken = default);
    Task RestoreStockAsync(int productId, int amount, CancellationToken cancellationToken = default);
}
