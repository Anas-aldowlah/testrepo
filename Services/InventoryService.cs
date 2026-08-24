using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;

namespace YAGOT_2._0.Services;

public class InventoryService : IInventoryService
{
    public const int MaximumStockQuantity = StockAdjustmentLimits.MaximumQuantity;

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
                         price.IsActive &&
                         price.SizeMl > 0 &&
                         price.Price > 0 &&
                         price.SizeMl < product.VolumeMl.Value,
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

    public async Task<StockAdjustmentResult> AdjustStockAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Include(candidate => candidate.RetailPrices)
            .SingleOrDefaultAsync(candidate => candidate.Id == request.ProductId, cancellationToken);
        if (product == null)
            return StockAdjustmentResult.MissingProduct;

        var calculation = CalculateStockAdjustment(product, request);
        if (!calculation.Succeeded)
            return StockAdjustmentResult.Failure(calculation.Field, calculation.Error!);
        var amount = calculation.Amount;

        var affectedRows = request.Operation == StockAdjustmentOperation.Subtract
            ? await _context.Products
                .Where(candidate => candidate.Id == request.ProductId && candidate.Stockquantity >= amount)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    candidate => candidate.Stockquantity,
                    candidate => candidate.Stockquantity - amount), cancellationToken)
            : await _context.Products
                .Where(candidate =>
                    candidate.Id == request.ProductId &&
                    candidate.Stockquantity <= MaximumStockQuantity - amount)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    candidate => candidate.Stockquantity,
                    candidate => candidate.Stockquantity + amount), cancellationToken);

        if (affectedRows == 1)
            return StockAdjustmentResult.Success;

        return request.Operation == StockAdjustmentOperation.Subtract
            ? StockAdjustmentResult.Failure(null, "لا يمكن الخصم لأن الرصيد الحالي أقل من قيمة التسوية. حدّث الصفحة وراجع الرصيد.")
            : StockAdjustmentResult.Failure(null, "لا يمكن الإضافة لأن الرصيد الناتج سيتجاوز 1,000,000. حدّث الصفحة وراجع الرصيد.");
    }

    public static StockAdjustmentCalculationResult CalculateStockAdjustment(Product product, StockAdjustmentRequest request)
    {
        if (!Enum.IsDefined(request.Operation))
            return StockAdjustmentCalculationResult.Failure(nameof(StockAdjustmentViewModel.Operation), "اختر إضافة أو خصماً صالحاً.");

        if (request.Quantity <= 0)
            return StockAdjustmentCalculationResult.Failure(nameof(StockAdjustmentViewModel.Quantity), "الكمية مطلوبة ويجب أن تكون أكبر من صفر.");

        var sizeResult = ResolveAdjustmentUnitSize(product, request);
        if (!sizeResult.Succeeded)
            return StockAdjustmentCalculationResult.Failure(sizeResult.Field, sizeResult.Error!);

        int amount;
        try
        {
            amount = checked(sizeResult.SizeMl * request.Quantity);
        }
        catch (OverflowException)
        {
            return StockAdjustmentCalculationResult.Failure(null, "قيمة تسوية المخزون تتجاوز النطاق الرقمي المدعوم.");
        }

        return amount is > 0 and <= MaximumStockQuantity
            ? StockAdjustmentCalculationResult.Success(amount)
            : StockAdjustmentCalculationResult.Failure(null, "يجب أن تكون قيمة تسوية المخزون بين 1 و1,000,000.");
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

    private static StockUnitSizeResult ResolveAdjustmentUnitSize(Product product, StockAdjustmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SizeOption))
            return StockUnitSizeResult.Failure(nameof(StockAdjustmentViewModel.SizeOption), "اختر حجماً صالحاً من القائمة.");

        if (!string.Equals(product.StockUnit, "Ml", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(request.SizeOption, StockAdjustmentSizeOptions.Base, StringComparison.OrdinalIgnoreCase)
                ? StockUnitSizeResult.Success(1)
                : StockUnitSizeResult.Failure(nameof(StockAdjustmentViewModel.SizeOption), "اختيار الحجم غير صالح لمنتج يُدار بالقطعة.");
        }

        if (product.VolumeMl is not > 0)
            return StockUnitSizeResult.Failure(null, "لا يمكن تسوية مخزون هذا المنتج لأن حجم العبوة الأساسي غير صالح.");

        if (string.Equals(request.SizeOption, StockAdjustmentSizeOptions.Base, StringComparison.OrdinalIgnoreCase))
            return StockUnitSizeResult.Success(product.VolumeMl.Value);

        if (string.Equals(request.SizeOption, StockAdjustmentSizeOptions.Custom, StringComparison.OrdinalIgnoreCase))
            return request.CustomSizeMl is > 0
                ? StockUnitSizeResult.Success(request.CustomSizeMl.Value)
                : StockUnitSizeResult.Failure(nameof(StockAdjustmentViewModel.CustomSizeMl), "الحجم المخصص مطلوب ويجب أن يكون أكبر من صفر.");

        if (request.SizeOption.StartsWith(StockAdjustmentSizeOptions.RetailPrefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(request.SizeOption[StockAdjustmentSizeOptions.RetailPrefix.Length..], out var retailPriceId))
        {
            var retailPrice = product.RetailPrices.FirstOrDefault(price =>
                price.Id == retailPriceId &&
                price.IsActive &&
                price.SizeMl > 0 &&
                price.SizeMl < product.VolumeMl.Value);
            return retailPrice != null && product.IsRetailEnabled
                ? StockUnitSizeResult.Success(retailPrice.SizeMl)
                : StockUnitSizeResult.Failure(nameof(StockAdjustmentViewModel.SizeOption), "حجم التجزئة المحدد غير صالح أو غير نشط.");
        }

        return StockUnitSizeResult.Failure(nameof(StockAdjustmentViewModel.SizeOption), "اختر حجماً صالحاً من القائمة.");
    }

    private sealed record StockUnitSizeResult(bool Succeeded, int SizeMl, string? Field = null, string? Error = null)
    {
        public static StockUnitSizeResult Success(int sizeMl) => new(true, sizeMl);
        public static StockUnitSizeResult Failure(string? field, string error) => new(false, 0, field, error);
    }
}
