using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.Services.Integration;

public sealed class SiteStateReconciliationBackgroundService : BackgroundService
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ISiteStateReconciliationCoordinator _coordinator;
    private readonly ISiteStateReconciliationPolicy _policy;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SiteStateReconciliationBackgroundService> _logger;

    public SiteStateReconciliationBackgroundService(
        IHostApplicationLifetime applicationLifetime,
        ISiteStateReconciliationCoordinator coordinator,
        ISiteStateReconciliationPolicy policy,
        TimeProvider timeProvider,
        ILogger<SiteStateReconciliationBackgroundService> logger)
    {
        _applicationLifetime = applicationLifetime;
        _coordinator = coordinator;
        _policy = policy;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await WaitForApplicationStartedAsync(stoppingToken);
            await Task.Delay(
                _policy.GetStartupDelay(),
                _timeProvider,
                stoppingToken);

            var consecutiveLocalFailures = 0;
            var startupSucceeded = await RunCycleAsync(
                SiteStateReconciliationReason.Startup,
                stoppingToken);
            consecutiveLocalFailures = startupSucceeded
                ? 0
                : consecutiveLocalFailures + 1;

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = consecutiveLocalFailures == 0
                    ? _policy.GetSuccessfulCycleDelay()
                    : _policy.GetFailedCycleDelay(consecutiveLocalFailures);
                await Task.Delay(delay, _timeProvider, stoppingToken);

                var succeeded = await RunCycleAsync(
                    SiteStateReconciliationReason.Periodic,
                    stoppingToken);
                consecutiveLocalFailures = succeeded
                    ? 0
                    : consecutiveLocalFailures + 1;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Site-state reconciliation background service stopped.");
        }
    }

    private async Task<bool> RunCycleAsync(
        SiteStateReconciliationReason reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var trigger = await _coordinator.RequestAsync(
                reason,
                cancellationToken);
            return trigger.Status == SiteStateReconciliationTriggerStatus.AlreadyRunning ||
                   trigger.Result?.Succeeded == true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Site-state reconciliation cycle failed unexpectedly; reason {Reason}; failure type {FailureType}.",
                reason,
                exception.GetType().Name);
            return false;
        }
    }

    private async Task WaitForApplicationStartedAsync(
        CancellationToken stoppingToken)
    {
        if (_applicationLifetime.ApplicationStarted.IsCancellationRequested)
        {
            return;
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var startedRegistration =
            _applicationLifetime.ApplicationStarted.Register(
                static state => ((TaskCompletionSource)state!).TrySetResult(),
                completion);
        using var stoppedRegistration = stoppingToken.Register(
            static state => ((TaskCompletionSource)state!).TrySetCanceled(),
            completion);
        await completion.Task;
    }
}
