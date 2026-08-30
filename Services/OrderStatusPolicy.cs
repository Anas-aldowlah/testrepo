namespace YAGOT_2._0.Services;

public static class OrderStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Processed = "Processed";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";
    public const string Refunded = "Refunded";
}

public static class OrderStatusPolicy
{
    private static readonly IReadOnlyDictionary<string, int> ForwardRanks =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [OrderStatuses.Paid] = 1,
            [OrderStatuses.Processed] = 2,
            [OrderStatuses.Shipped] = 3,
            [OrderStatuses.Delivered] = 4
        };

    public static readonly string[] DisplayStatuses =
    [
        OrderStatuses.Pending,
        OrderStatuses.Paid,
        OrderStatuses.Processed,
        OrderStatuses.Shipped,
        OrderStatuses.Delivered,
        OrderStatuses.Cancelled,
        OrderStatuses.Refunded
    ];

    public static string? Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        return status.Trim().ToLowerInvariant() switch
        {
            "pending" or "قيد الانتظار" => OrderStatuses.Pending,
            "paid" or "تم الدفع" => OrderStatuses.Paid,
            "processed" or "processing" or "قيد المعالجة" or "جاري التجهيز" => OrderStatuses.Processed,
            "shipped" or "تم الشحن" => OrderStatuses.Shipped,
            "delivered" or "completed" or "تم التوصيل" or "مكتمل" => OrderStatuses.Delivered,
            "cancelled" or "canceled" or "ملغي" => OrderStatuses.Cancelled,
            "refunded" or "مرتجع" or "مسترجع" => OrderStatuses.Refunded,
            _ => null
        };
    }

    public static bool CanTransition(string currentStatus, string requestedStatus)
    {
        var current = Normalize(currentStatus);
        var requested = Normalize(requestedStatus);
        if (current == null || requested == null)
            return false;
        if (string.Equals(current, requested, StringComparison.Ordinal))
            return true;
        if (current is OrderStatuses.Delivered or OrderStatuses.Cancelled or OrderStatuses.Refunded)
            return false;
        if (requested == OrderStatuses.Cancelled)
            return current is OrderStatuses.Pending or OrderStatuses.Paid or OrderStatuses.Processed or OrderStatuses.Shipped;

        return ForwardRanks.TryGetValue(current, out var currentRank) &&
               ForwardRanks.TryGetValue(requested, out var requestedRank) &&
               requestedRank > currentRank;
    }

    public static IReadOnlyList<string> GetAllowedTargets(string currentStatus)
    {
        return DisplayStatuses
            .Where(status => !string.Equals(status, currentStatus, StringComparison.OrdinalIgnoreCase) &&
                             CanTransition(currentStatus, status))
            .ToArray();
    }
}
