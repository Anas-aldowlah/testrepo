using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;

namespace YAGOT_2._0.Services;

public interface IInventoryService
{
    int CalculateDeductionAmount(Product product, int quantity, int? retailSizeMl);
    int GetAvailableSaleUnits(Product product, int? retailSizeMl);
    decimal GetUnitPrice(Product product, ProductRetailPrice? retailPrice);
    Task<ProductRetailPrice?> ValidateRetailPriceAsync(Product product, int? retailPriceId, CancellationToken cancellationToken = default);
    Task DeductStockAsync(int productId, int amount, CancellationToken cancellationToken = default);
    Task RestoreStockAsync(int productId, int amount, CancellationToken cancellationToken = default);
    Task<StockAdjustmentResult> AdjustStockAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default);
}

public sealed record StockAdjustmentRequest(
    int ProductId,
    StockAdjustmentOperation Operation,
    string SizeOption,
    int? CustomSizeMl,
    int Quantity);

public sealed record StockAdjustmentResult(bool Succeeded, bool ProductFound = true, string? Field = null, string? Error = null)
{
    public static StockAdjustmentResult Success { get; } = new(true);
    public static StockAdjustmentResult MissingProduct { get; } = new(false, false);
    public static StockAdjustmentResult Failure(string? field, string error) => new(false, true, field, error);
}

public sealed record StockAdjustmentCalculationResult(bool Succeeded, int Amount, string? Field = null, string? Error = null)
{
    public static StockAdjustmentCalculationResult Success(int amount) => new(true, amount);
    public static StockAdjustmentCalculationResult Failure(string? field, string error) => new(false, 0, field, error);
}
