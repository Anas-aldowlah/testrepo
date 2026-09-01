using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public sealed class BestSellerService : IBestSellerService
{
    private static readonly SemaphoreSlim _syncLock = new(1, 1);
    private static readonly string[] ValidOnlineStatuses = { "Paid", "Processed", "Shipped", "Delivered" };

    private readonly NeondbContext _context;
    private readonly ILogger<BestSellerService> _logger;

    public BestSellerService(NeondbContext context, ILogger<BestSellerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BestSellerRefreshResult> RefreshBestSellersAsync(CancellationToken cancellationToken = default)
    {
        var isLockAcquired = await _syncLock.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        if (!isLockAcquired)
        {
            _logger.LogWarning("Best Sellers refresh skipped: another calculation is already in progress.");
            return new BestSellerRefreshResult(
                Success: false,
                ProductsUpdated: 0,
                OnlineUnitsSold: 0,
                PosUnitsSold: 0,
                TotalUnitsSold: 0,
                Duration: TimeSpan.Zero,
                Timestamp: DateTime.UtcNow,
                ErrorMessage: "عملية تحديث الأكثر مبيعاً قيد التشغيل حالياً بواسطة عملية أخرى."
            );
        }

        var stopwatch = Stopwatch.StartNew();
        var refreshTimestamp = DateTime.UtcNow;
        var cutoffDate = refreshTimestamp.AddDays(-BestSellerConstants.BestSellerPeriodDays);

        _logger.LogInformation(
            "Starting Best Sellers calculation for the rolling {PeriodDays}-day window (from {CutoffDate:yyyy-MM-dd HH:mm:ss} UTC).",
            BestSellerConstants.BestSellerPeriodDays,
            cutoffDate);

        try
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                // 1. حساب مبيعات المتجر الإلكتروني المؤكدة خلال آخر 60 يوماً
                var onlineSales = await _context.Orderitems
                    .AsNoTracking()
                    .Where(oi => oi.Order.Orderdate >= cutoffDate &&
                                 oi.Order.Stockdeducted &&
                                 ValidOnlineStatuses.Contains(oi.Order.Status))
                    .GroupBy(oi => oi.Productid)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        UnitsSold = g.Sum(x => x.FulfilledQuantity ?? x.Quantity)
                    })
                    .ToDictionaryAsync(x => x.ProductId, x => x.UnitsSold, cancellationToken);

                // 2. حساب مبيعات نقاط البيع والكاشير المكتملة خلال آخر 60 يوماً
                var posSales = await _context.SaleItems
                    .AsNoTracking()
                    .Where(si => si.Sale.Status == "Completed" &&
                                 (si.Sale.CompletedAt ?? si.Sale.CreatedAt) >= cutoffDate)
                    .GroupBy(si => si.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        UnitsSold = g.Sum(x => x.Quantity)
                    })
                    .ToDictionaryAsync(x => x.ProductId, x => x.UnitsSold, cancellationToken);

                // 3. دمج المبيعات المجمعة لكل منتج
                var mergedSales = new Dictionary<int, int>();
                var totalOnlineUnits = 0;
                var totalPosUnits = 0;

                foreach (var (productId, units) in onlineSales)
                {
                    mergedSales[productId] = units;
                    totalOnlineUnits += units;
                }

                foreach (var (productId, units) in posSales)
                {
                    mergedSales[productId] = mergedSales.GetValueOrDefault(productId, 0) + units;
                    totalPosUnits += units;
                }

                var totalUnits = totalOnlineUnits + totalPosUnits;

                // 4. تحديث جدول المنتجات داخل Transaction آمن
                using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

                var products = await _context.Products.ToListAsync(cancellationToken);
                foreach (var product in products)
                {
                    product.TotalSold = mergedSales.GetValueOrDefault(product.Id, 0);
                    product.SalesLastUpdatedAt = refreshTimestamp;
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                stopwatch.Stop();

                _logger.LogInformation(
                    "Best Sellers refresh completed successfully: {ProductCount} products updated ({OnlineUnits} online units + {PosUnits} POS units = {TotalUnits} total units) in {ElapsedMs}ms.",
                    products.Count,
                    totalOnlineUnits,
                    totalPosUnits,
                    totalUnits,
                    stopwatch.ElapsedMilliseconds);

                return new BestSellerRefreshResult(
                    Success: true,
                    ProductsUpdated: products.Count,
                    OnlineUnitsSold: totalOnlineUnits,
                    PosUnitsSold: totalPosUnits,
                    TotalUnitsSold: totalUnits,
                    Duration: stopwatch.Elapsed,
                    Timestamp: refreshTimestamp
                );
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to refresh Best Sellers data after {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);

            return new BestSellerRefreshResult(
                Success: false,
                ProductsUpdated: 0,
                OnlineUnitsSold: 0,
                PosUnitsSold: 0,
                TotalUnitsSold: 0,
                Duration: stopwatch.Elapsed,
                Timestamp: refreshTimestamp,
                ErrorMessage: ex.Message
            );
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<DateTime?> GetLastRefreshTimeAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(p => p.SalesLastUpdatedAt != null)
            .MaxAsync(p => p.SalesLastUpdatedAt, cancellationToken);
    }
}
