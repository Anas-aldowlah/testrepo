using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace YAGOT_2._0.Services;

/// <summary>
/// خدمة مراقبة صحة وجاهزية قواعد البيانات للقراءة فقط (Read-Only Health Monitor).
/// لا تقوم بأي عمليات DDL أو ترقيات تلقائية أثناء تشغيل خادم الويب لتفادي قفل الجداول أو الانقسام الجزئي.
/// </summary>
public sealed class MigrationBackgroundService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MigrationStateTracker _tracker;
    private readonly ILogger<MigrationBackgroundService> _logger;

    public MigrationBackgroundService(
        IServiceScopeFactory scopeFactory,
        MigrationStateTracker tracker,
        ILogger<MigrationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MigrationBackgroundService: بدء خدمة مراقبة جاهزية قواعد البيانات للقراءة فقط.");

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var coordinator = scope.ServiceProvider.GetRequiredService<DatabaseMigrationCoordinator>();

                    var (isReady, reason) = await coordinator.VerifyReadinessAsync(stoppingToken);
                    if (!isReady)
                    {
                        _logger.LogWarning("MigrationBackgroundService: رصد عدم جاهزية أو ترقيات معلقة لقواعد البيانات: {Reason}", reason);
                    }
                    else
                    {
                        _logger.LogDebug("MigrationBackgroundService: فحص الجاهزية الدوري ناجح.");
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MigrationBackgroundService: حدث خطأ أثناء فحص جاهزية قواعد البيانات.");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("MigrationBackgroundService: إيقاف خدمة المراقبة مع إيقاف التطبيق.");
        }
    }
}
