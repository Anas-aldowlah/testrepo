using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Integration.Capabilities;

namespace YAGOT_2._0.Services.Integration;

public sealed class CapabilityReconciliationBackgroundService(
    IHostApplicationLifetime applicationLifetime,
    IServiceScopeFactory scopeFactory,
    IOptions<CapabilityReconciliationOptions> options,
    TimeProvider timeProvider,
    ILogger<CapabilityReconciliationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await WaitForApplicationStartedAsync(stoppingToken);
            // Slight initial delay to allow the application server and local network to settle
            await Task.Delay(TimeSpan.FromSeconds(2), timeProvider, stoppingToken);

            var consecutiveFailures = 0;
            var startupSucceeded = await RunCycleAsync(CapabilityReconciliationReason.Startup, stoppingToken);
            consecutiveFailures = startupSucceeded ? 0 : consecutiveFailures + 1;

            var intervalMinutes = Math.Max(1, options.Value.ReconciliationIntervalMinutes);
            var standardDelay = TimeSpan.FromMinutes(intervalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = consecutiveFailures == 0
                    ? standardDelay
                    : TimeSpan.FromSeconds(Math.Min(300, 10 * Math.Pow(2, Math.Min(consecutiveFailures, 5))));

                await Task.Delay(delay, timeProvider, stoppingToken);

                var succeeded = await RunCycleAsync(CapabilityReconciliationReason.Periodic, stoppingToken);
                consecutiveFailures = succeeded ? 0 : consecutiveFailures + 1;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Capability reconciliation background service stopped.");
        }
    }

    private async Task<bool> RunCycleAsync(CapabilityReconciliationReason reason, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ICapabilityReconciliationService>();
            var result = await service.ReconcileAsync(reason, cancellationToken);
            return result.Succeeded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Capability reconciliation cycle failed unexpectedly; reason {Reason}.", reason);
            return false;
        }
    }

    private async Task WaitForApplicationStartedAsync(CancellationToken stoppingToken)
    {
        if (applicationLifetime.ApplicationStarted.IsCancellationRequested)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var regStarted = applicationLifetime.ApplicationStarted.Register(
            static s => ((TaskCompletionSource)s!).TrySetResult(), completion);
        using var regStopped = stoppingToken.Register(
            static s => ((TaskCompletionSource)s!).TrySetCanceled(), completion);
        await completion.Task;
    }
}
