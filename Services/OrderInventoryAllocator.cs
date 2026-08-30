namespace YAGOT_2._0.Services;

public sealed record OrderAllocationLine(
    int OrderitemId,
    int ProductId,
    int RequestedQuantity,
    int StockPerUnit,
    decimal HistoricalUnitPrice);

public sealed record OrderAllocationResult(
    int OrderitemId,
    int ProductId,
    int RequestedQuantity,
    int FulfillableQuantity,
    int UnavailableQuantity,
    int StockPerUnit,
    decimal HistoricalUnitPrice);

public static class OrderInventoryAllocator
{
    public static IReadOnlyList<OrderAllocationResult> Allocate(
        IEnumerable<OrderAllocationLine> lines,
        IReadOnlyDictionary<int, int> availableStockByProduct)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(availableStockByProduct);

        var orderedLines = lines.OrderBy(line => line.OrderitemId).ToArray();
        var remainingStock = new Dictionary<int, int>();
        var results = new List<OrderAllocationResult>(orderedLines.Length);

        foreach (var line in orderedLines)
        {
            if (line.OrderitemId <= 0 || line.ProductId <= 0 || line.RequestedQuantity < 0 || line.StockPerUnit <= 0)
                throw new InvalidOperationException("Invalid order inventory allocation input.");

            if (!remainingStock.TryGetValue(line.ProductId, out var available))
            {
                available = Math.Max(0, availableStockByProduct.GetValueOrDefault(line.ProductId));
                remainingStock.Add(line.ProductId, available);
            }

            var fulfillable = Math.Min(line.RequestedQuantity, available / line.StockPerUnit);
            var unavailable = line.RequestedQuantity - fulfillable;
            remainingStock[line.ProductId] = checked(available - checked(fulfillable * line.StockPerUnit));
            results.Add(new OrderAllocationResult(
                line.OrderitemId,
                line.ProductId,
                line.RequestedQuantity,
                fulfillable,
                unavailable,
                line.StockPerUnit,
                line.HistoricalUnitPrice));
        }

        return results.OrderBy(result => result.OrderitemId).ToArray();
    }

    public static decimal CalculateFulfilledValue(IEnumerable<OrderAllocationResult> allocation)
    {
        decimal value = 0m;
        checked
        {
            foreach (var line in allocation)
                value += line.FulfillableQuantity * line.HistoricalUnitPrice;
        }

        return value;
    }
}

public static class OrderWorkflowStates
{
    public const string ConflictAwaitingDecision = "ConflictAwaitingDecision";
    public const string ConflictResolvedContinue = "ConflictResolvedContinue";
    public const string ConflictResolvedCancel = "ConflictResolvedCancel";
}
