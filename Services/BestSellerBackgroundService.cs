using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace YAGOT_2._0.Services;

public sealed class BestSellerBackgroundService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan InitialStartupDelay = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BestSellerBackgroundService> _logger;

    public BestSellerBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BestSellerBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BestSellerBackgroundService is starting (Refresh interval: {Hours} hours).", RefreshInterval.TotalHours);

        try
        {
            // تأخير أولي لمنح التطبيق فرصة إنهاء الإقلاع وتهيئة قاعدة البيانات
            await Task.Delay(InitialStartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await ExecuteRefreshAsync(stoppingToken);

                // الانتظار لمدة 6 ساعات قبل التحديث التالي
                await Task.Delay(RefreshInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("BestSellerBackgroundService is stopping gracefully due to host shutdown.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred in BestSellerBackgroundService loop.");
        }
    }

    private async Task ExecuteRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("BestSellerBackgroundService: Starting scheduled Best Sellers refresh.");

            using var scope = _scopeFactory.CreateScope();
            var bestSellerService = scope.ServiceProvider.GetRequiredService<IBestSellerService>();

            var result = await bestSellerService.RefreshBestSellersAsync(cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "BestSellerBackgroundService: Scheduled refresh completed successfully. {ProductsUpdated} products updated in {ElapsedMs}ms.",
                    result.ProductsUpdated,
                    result.Duration.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "BestSellerBackgroundService: Scheduled refresh completed with warning/failure: {ErrorMessage}",
                    result.ErrorMessage);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BestSellerBackgroundService: Error during scheduled Best Sellers calculation.");
        }
    }
}
