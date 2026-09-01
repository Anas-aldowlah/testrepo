using System;
using System.Threading;
using System.Threading.Tasks;

namespace YAGOT_2._0.Services;

public static class BestSellerConstants
{
    public const int BestSellerPeriodDays = 60;
    public const int DefaultDisplayCount = 8;
}

public sealed record BestSellerRefreshResult(
    bool Success,
    int ProductsUpdated,
    int OnlineUnitsSold,
    int PosUnitsSold,
    int TotalUnitsSold,
    TimeSpan Duration,
    DateTime Timestamp,
    string? ErrorMessage = null
);

public interface IBestSellerService
{
    Task<BestSellerRefreshResult> RefreshBestSellersAsync(CancellationToken cancellationToken = default);
    Task<DateTime?> GetLastRefreshTimeAsync(CancellationToken cancellationToken = default);
}
